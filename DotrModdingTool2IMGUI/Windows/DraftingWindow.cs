using System.Numerics;
using ImGuiNET;

namespace DotrModdingTool2IMGUI;

public class DraftingWindow : IImGuiWindow
{
    enum BudgetStatus
    {
        Good,
        Tight,
        Critical
    }

    enum DraftRarity
    {
        Common,
        Uncommon,
        Rare,
        SuperRare
    }

    class DraftSettings
    {
        public bool GroupSameRarity = true;
        public bool GroupSimilarRarityScore = true;
        public bool GroupSameType;
        public float SameRarityOfferChance = 0.55f;
        public float SimilarRarityScoreOfferChance = 0.35f;
        public float SimilarRarityScoreRange = 12f;
        public float SameTypeOfferChance = 0.25f;
        public float CommonWeight = 60f;
        public float UncommonWeight = 30f;
        public float RareWeight = 9f;
        public float SuperRareWeight = 1f;
    }

    static readonly int[] BudgetDeckIndices = { 28, 29, 37 };
    static readonly HashSet<ushort> BaseBannedCardIndices = new HashSet<ushort> { 671, 699, 700, 714, 732, 733, 751, 789, 827, 829 };
    static readonly HashSet<ushort> SuperRareSpellIndices = new HashSet<ushort> { 684, 685, 686, 699, 701 };
    static readonly HashSet<ushort> RareSpellIndices = new HashSet<ushort> { 713 };
    static readonly HashSet<ushort> UncommonSpellIndices = new HashSet<ushort> { 716, 736, 746, 795, 796 };
    static readonly HashSet<ushort> CommonTrapIndices = new HashSet<ushort> { 807, 808, 813, 814, 815, 816, 817, 820, 821, 822, 824, 826, 828 };
    static readonly HashSet<ushort> UncommonTrapIndices = new HashSet<ushort> { 805, 809, 818, 823 };
    static readonly HashSet<ushort> RareTrapIndices = new HashSet<ushort> { 801, 803, 804, 810, 819 };
    static readonly HashSet<ushort> SuperRareTrapIndices = new HashSet<ushort> { 802, 806, 811, 812, 825, 827, 829 };
    readonly Random random = new Random();
    readonly CardConstant?[] currentChoices = new CardConstant?[3];
    readonly DraftSettings settings = new DraftSettings();
    readonly Dictionary<ushort, float> cardRarityScoreOverrides = new Dictionary<ushort, float>();
    readonly Dictionary<ushort, string> cardTypeOverrides = new Dictionary<ushort, string>();
    string saveStatus = "";

    public Deck DraftDeck { get; } = new Deck();

    public void Render()
    {
        if (!DataAccess.Instance.IsIsoLoaded)
        {
            ImGui.Text("Please load ISO file");
            return;
        }

        EnsureChoices();

        if (ImGui.BeginTabBar("DraftingWindowTabs"))
        {
            if (ImGui.BeginTabItem("Draft"))
            {
                RenderDraft();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                RenderSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    void RenderDraft()
    {
        ImGui.Text($"Draft deck: {DraftDeck.CardList.Count} / 40");
        ImGui.SameLine();
        ImGui.Text($"Total DC {DraftDeck.DeckCost} / {MaxDraftDeckCost}");
        ImGui.SameLine();
        ImGui.Text($"Remaining DC {RemainingDraftBudget}");
        ImGui.SameLine();
        ImGui.Text($"Budget {CurrentBudgetStatus}");
        ImGui.SameLine();
        if (ImGui.Button("Reset draft"))
        {
            DraftDeck.CardList.Clear();
            saveStatus = "";
            DrawNewChoices();
        }
        ImGui.SameLine();
        if (DraftDeck.CardList.Count == 40 && IsDraftDeckUnderBudget)
        {
            if (ImGui.Button("Save over starter decks"))
            {
                SaveOverStarterDecks();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Save over starter decks");
            ImGui.EndDisabled();
        }

        if (!string.IsNullOrWhiteSpace(saveStatus))
        {
            ImGui.Text(saveStatus);
        }

        ImGui.Separator();

        if (DraftDeck.CardList.Count < 40 && currentChoices.All(choice => choice == null))
        {
            ImGui.Text("No legal cards can fit the remaining deck cost.");
            return;
        }

        float availableHeight = ImGui.GetContentRegionAvail().Y;
        float choicesHeight = Math.Min(260f, availableHeight * 0.45f);

        for (int i = 0; i < currentChoices.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            DrawChoice(i, choicesHeight);
        }

        ImGui.Separator();
        DrawDraftDeckTable();
    }

    void RenderSettings()
    {
        bool changed = false;

        changed |= ImGui.Checkbox("Group same rarity", ref settings.GroupSameRarity);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Same rarity chance", ref settings.SameRarityOfferChance, 0f, 1f);

        changed |= ImGui.Checkbox("Group similar rarity score", ref settings.GroupSimilarRarityScore);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Similar score chance", ref settings.SimilarRarityScoreOfferChance, 0f, 1f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Similar score range", ref settings.SimilarRarityScoreRange, 1f, 50f);

        changed |= ImGui.Checkbox("Group same draft type", ref settings.GroupSameType);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Same type chance", ref settings.SameTypeOfferChance, 0f, 1f);

        ImGui.Separator();
        ImGui.Text("Rarity weights");

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Common", ref settings.CommonWeight, 0f, 100f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Uncommon", ref settings.UncommonWeight, 0f, 100f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Rare", ref settings.RareWeight, 0f, 100f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.SliderFloat("Super Rare", ref settings.SuperRareWeight, 0f, 100f);

        if (changed)
        {
            ClampSettings();
        }

        if (ImGui.Button("Reroll current offer"))
        {
            DrawNewChoices();
        }
    }

    public void Free()
    {
    }

    void EnsureChoices()
    {
        if (DraftDeck.CardList.Count < 40 && currentChoices.Any(choice => choice == null))
        {
            DrawNewChoices();
        }
    }

    void DrawNewChoices()
    {
        List<CardConstant> availableCards = GetLegalNextCards()
            .ToList();

        Array.Fill(currentChoices, null);

        if (CurrentBudgetStatus == BudgetStatus.Critical)
        {
            List<CardConstant> budgetFriendlyCards = availableCards
                .Where(card => card.DeckCost <= RemainingAverageDeckCost)
                .ToList();
            List<CardConstant>? groupedBudgetFriendlyCards = TryGetGroupedOfferPool(budgetFriendlyCards);
            List<CardConstant> criticalPool = groupedBudgetFriendlyCards ?? budgetFriendlyCards;

            for (int i = 0; i < 2; i++)
            {
                CardConstant? budgetCard = DrawWeightedCard(criticalPool);
                if (budgetCard == null)
                {
                    break;
                }

                currentChoices[i] = budgetCard;
                availableCards.Remove(budgetCard);
                budgetFriendlyCards.Remove(budgetCard);
            }
        }

        List<CardConstant>? groupedCards = CurrentBudgetStatus == BudgetStatus.Critical
            ? null
            : TryGetGroupedOfferPool(availableCards);

        for (int i = 0; i < currentChoices.Length; i++)
        {
            if (currentChoices[i] != null)
            {
                continue;
            }

            if (availableCards.Count == 0)
            {
                return;
            }

            List<CardConstant> drawPool = groupedCards is { Count: > 0 } ? groupedCards : availableCards;
            CardConstant? selectedCard = DrawWeightedCard(drawPool);
            if (selectedCard == null)
            {
                return;
            }

            currentChoices[i] = selectedCard;
            availableCards.Remove(selectedCard);
        }
    }

    void DrawChoice(int choiceIndex, float height)
    {
        CardConstant? card = currentChoices[choiceIndex];
        Vector2 choiceSize = new Vector2(ImGui.GetContentRegionAvail().X / (3 - choiceIndex), height);

        ImGui.PushID(choiceIndex);
        ImGui.BeginChild("DraftChoice", choiceSize, ImGuiChildFlags.Border | ImGuiChildFlags.AlwaysAutoResize);

        if (card == null)
        {
            ImGui.Text("No legal card");
            ImGui.EndChild();
            ImGui.PopID();
            return;
        }

        ImGui.TextWrapped(card.Name.Current);
        ImGui.Text($"{GetCardRarity(card)} {GetCardRarityScore(card):0} / {GetDraftCardType(card)}");
        ImGui.Text($"ATK {card.Attack} / DEF {card.Defense}");
        ImGui.Text($"LVL {card.Level} / DC {card.DeckCost}");

        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImageHelper.DefaultImageSize.X) / 2f);
        ImGui.Image(GlobalImages.Instance.Cards[card.Name.Default], ImageHelper.DefaultImageSize);
        if (ImGui.IsItemHovered())
        {
            GlobalImgui.RenderTooltipCardImage(card.Name.Default);
        }

        if (DraftDeck.CardList.Count >= 40)
        {
            ImGui.Text("Draft deck is full");
        }
        else if (ImGui.Button("Choose", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
        {
            DraftDeck.CardList.Add(new DeckCard(card, DeckLeaderRank.NCO));
            saveStatus = "";
            if (DraftDeck.CardList.Count < 40)
            {
                DrawNewChoices();
            }
            else
            {
                Array.Fill(currentChoices, null);
            }
        }

        ImGui.EndChild();
        ImGui.PopID();
    }

    void DrawDraftDeckTable()
    {
        ImGui.Text("Picked cards");

        if (ImGui.BeginTable("DraftDeck", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("ATK");
            ImGui.TableSetupColumn("DEF");
            ImGui.TableSetupColumn("LVL");
            ImGui.TableSetupColumn("Attribute");
            ImGui.TableSetupColumn("Type");
            ImGui.TableSetupColumn("DC");
            ImGui.TableHeadersRow();

            for (int i = 0; i < DraftDeck.CardList.Count; i++)
            {
                CardConstant card = DraftDeck.CardList[i].CardConstant;
                Vector4 colour = DeckEditorWindow.CardConstantRowColor(card).value;
                uint rowColor = (uint)((int)(colour.W * 255) << 24 | (int)(colour.Z * 255) << 16 | (int)(colour.Y * 255) << 8 |
                                       (int)(colour.X * 255));

                ImGui.TableNextRow();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, rowColor);

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(card.Name.Current);

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(card.Attack.ToString());

                ImGui.TableSetColumnIndex(2);
                ImGui.Text(card.Defense.ToString());

                ImGui.TableSetColumnIndex(3);
                ImGui.Text(card.Level.ToString());

                ImGui.TableSetColumnIndex(4);
                ImGui.Text(card.AttributeName);

                ImGui.TableSetColumnIndex(5);
                ImGui.Text(card.Type);

                ImGui.TableSetColumnIndex(6);
                ImGui.Text(card.DeckCost.ToString());

                ImGui.TableSetColumnIndex(0);
                if (ImGui.Selectable($"##draft-card-{i}", false, ImGuiSelectableFlags.SpanAllColumns))
                {
                }
                if (ImGui.IsItemHovered())
                {
                    GlobalImgui.RenderTooltipCardImage(card.Name.Default);
                }
            }

            ImGui.EndTable();
        }
    }

    void SaveOverStarterDecks()
    {
        if (DraftDeck.CardList.Count != 40)
        {
            saveStatus = "Draft deck must have exactly 40 cards before saving.";
            return;
        }

        if (!IsDraftDeckUnderBudget)
        {
            saveStatus = $"Draft deck must have less DC than Weevil, Rex, and Tea. Current: {DraftDeck.DeckCost}, max: {MaxDraftDeckCost}.";
            return;
        }

        if (DraftDeck.CardList.Any(card => !IsDraftableCard(card.CardConstant)))
        {
            saveStatus = "Draft deck contains banned cards.";
            return;
        }

        for (int deckIndex = 0; deckIndex <= 16; deckIndex++)
        {
            Deck deck = Deck.DeckList[deckIndex];
            deck.CardList = DraftDeck.CardList
                .Select(card => new DeckCard(card.CardConstant, DeckLeaderRank.NCO))
                .ToList();

            DataAccess.Instance.SaveDeck(deckIndex, deck.Bytes);
        }

        UpdateStartingDeck.CreateNewStartingDeckData(Deck.DeckList);
        saveStatus = "Saved draft deck over starter decks 0-16.";
    }

    IEnumerable<CardConstant> GetLegalNextCards()
    {
        return DraftableCards.Where(CanFinishDeckAfterPicking);
    }

    CardConstant? DrawWeightedCard(List<CardConstant> cards)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        double totalWeight = cards.Sum(GetCardWeight);
        double roll = random.NextDouble() * totalWeight;

        for (int i = 0; i < cards.Count; i++)
        {
            roll -= GetCardWeight(cards[i]);
            if (roll <= 0)
            {
                CardConstant selectedCard = cards[i];
                cards.RemoveAt(i);
                return selectedCard;
            }
        }

        CardConstant fallbackCard = cards[^1];
        cards.RemoveAt(cards.Count - 1);
        return fallbackCard;
    }

    double GetCardWeight(CardConstant card)
    {
        float remainingAverage = RemainingAverageDeckCost;
        float overAverage = Math.Max(0, card.DeckCost - remainingAverage);
        float underAverage = Math.Max(0, remainingAverage - card.DeckCost);
        double rarityWeight = Math.Max(0.01f, GetRarityScoreWeight(GetCardRarityScore(card)));

        switch (CurrentBudgetStatus)
        {
            case BudgetStatus.Good:
                return rarityWeight;
            case BudgetStatus.Tight:
                return rarityWeight * Math.Max(1, 25 - overAverage * 1.5f + underAverage * 0.25f);
            case BudgetStatus.Critical:
                return rarityWeight * Math.Max(1, 35 - overAverage * 4f + underAverage * 0.75f);
            default:
                return rarityWeight;
        }
    }

    List<CardConstant>? TryGetGroupedOfferPool(List<CardConstant> cards)
    {
        if (cards.Count < 2)
        {
            return null;
        }

        if (settings.GroupSameType && random.NextDouble() < settings.SameTypeOfferChance)
        {
            List<CardConstant>? typeGroup = TryGetTypeGroup(cards);
            if (typeGroup != null)
            {
                return typeGroup;
            }
        }

        if (settings.GroupSimilarRarityScore && random.NextDouble() < settings.SimilarRarityScoreOfferChance)
        {
            List<CardConstant>? scoreGroup = TryGetSimilarRarityScoreGroup(cards);
            if (scoreGroup != null)
            {
                return scoreGroup;
            }
        }

        if (settings.GroupSameRarity && random.NextDouble() < settings.SameRarityOfferChance)
        {
            return TryGetRarityGroup(cards);
        }

        return null;
    }

    List<CardConstant>? TryGetRarityGroup(List<CardConstant> cards)
    {
        CardConstant? anchor = DrawWeightedCard(cards.ToList());
        if (anchor == null)
        {
            return null;
        }

        List<CardConstant> groupedCards = cards
            .Where(card => GetCardRarity(card) == GetCardRarity(anchor))
            .ToList();

        return groupedCards.Count >= 2 ? groupedCards : null;
    }

    List<CardConstant>? TryGetSimilarRarityScoreGroup(List<CardConstant> cards)
    {
        CardConstant? anchor = DrawWeightedCard(cards.ToList());
        if (anchor == null)
        {
            return null;
        }

        float anchorScore = GetCardRarityScore(anchor);
        List<CardConstant> groupedCards = cards
            .Where(card => Math.Abs(GetCardRarityScore(card) - anchorScore) <= settings.SimilarRarityScoreRange)
            .ToList();

        return groupedCards.Count >= 2 ? groupedCards : null;
    }

    List<CardConstant>? TryGetTypeGroup(List<CardConstant> cards)
    {
        CardConstant? anchor = DrawWeightedCard(cards.ToList());
        if (anchor == null)
        {
            return null;
        }

        List<CardConstant> groupedCards = cards
            .Where(card => GetDraftCardType(card) == GetDraftCardType(anchor))
            .ToList();

        return groupedCards.Count >= 2 ? groupedCards : null;
    }

    DraftRarity GetCardRarity(CardConstant card)
    {
        float rarityScore = GetCardRarityScore(card);

        if (rarityScore < 25)
        {
            return DraftRarity.Common;
        }

        if (rarityScore < 50)
        {
            return DraftRarity.Uncommon;
        }

        if (rarityScore < 75)
        {
            return DraftRarity.Rare;
        }

        return DraftRarity.SuperRare;
    }

    float GetCardRarityScore(CardConstant card)
    {
        if (cardRarityScoreOverrides.TryGetValue(card.Index, out float rarityScore))
        {
            return rarityScore;
        }

        if (IsManualRareCard(card))
        {
            return 72f;
        }

        if (card.CardKind.isRitual())
        {
            return 82f;
        }

        if (card.CardKind.isMagic() || card.CardKind.isTrap())
        {
            return card.CardKind.isTrap()
                ? GetTrapRarityScore(card)
                : GetSpellRarityScore(card);
        }

        if (card.CardKind.isPowerUp() || IsEquipmentCard(card))
        {
            return 48f;
        }

        if (card.CardKind.isMonster())
        {
            return GetMonsterRarityScore(card);
        }

        return 25f;
    }

    float GetSpellRarityScore(CardConstant card)
    {
        if (SuperRareSpellIndices.Contains(card.Index))
        {
            return ScoreWithVariation(88f, card.Index, 0.1f);
        }

        if (card.DeckCost > 60)
        {
            return ScoreWithVariation(93f, card.Index, 0.1f);
        }

        if (card.DeckCost > 50)
        {
            return ScoreWithVariation(86f, card.Index, 0.1f);
        }

        if (RareSpellIndices.Contains(card.Index) || IsRareSpellRange(card.Index))
        {
            return ScoreWithVariation(63f, card.Index, 0.1f);
        }

        if (UncommonSpellIndices.Contains(card.Index))
        {
            return ScoreWithVariation(38f, card.Index, 0.1f);
        }

        return ScoreWithVariation(13f, card.Index, 0.1f);
    }

    bool IsRareSpellRange(ushort cardIndex)
    {
        return (cardIndex >= 689 && cardIndex <= 697)
               || (cardIndex >= 720 && cardIndex <= 731);
    }

    float GetTrapRarityScore(CardConstant card)
    {
        if (SuperRareTrapIndices.Contains(card.Index))
        {
            return ScoreWithVariation(88f, card.Index, 0.1f);
        }

        if (RareTrapIndices.Contains(card.Index))
        {
            return ScoreWithVariation(63f, card.Index, 0.1f);
        }

        if (UncommonTrapIndices.Contains(card.Index))
        {
            return ScoreWithVariation(38f, card.Index, 0.1f);
        }

        if (CommonTrapIndices.Contains(card.Index))
        {
            return ScoreWithVariation(13f, card.Index, 0.1f);
        }

        return ScoreWithVariation(13f, card.Index, 0.1f);
    }

    float GetMonsterRarityScore(CardConstant card)
    {
        float score;

        if (card.Attack >= 2300)
        {
            score = 82f;
        }
        else if (card.Attack >= 2100)
        {
            score = 70f;
        }
        else if (card.Attack >= 1900)
        {
            score = 58f;
        }
        else if (card.Attack >= 1700)
        {
            score = 46f;
        }
        else if (card.Attack >= 1500)
        {
            score = 34f;
        }
        else if (card.Attack >= 1200)
        {
            score = 24f;
        }
        else
        {
            score = 12f;
        }

        if (card.Level <= 3)
        {
            score += 12f;
        }
        else if (card.Level == 4)
        {
            score += 8f;
        }
        else if (card.Level == 5)
        {
            score += 2f;
        }
        else if (card.Level == 6)
        {
            score -= 6f;
        }
        else
        {
            score -= 14f;
        }

        if (card.Defense >= 2200)
        {
            score += 14f;
        }
        else if (card.Defense >= 2000)
        {
            score += 8f;
        }
        else if (card.Defense >= 1800)
        {
            score += 3f;
        }

        return Math.Clamp(score, 1f, 95f);
    }

    bool IsManualRareCard(CardConstant card)
    {
        string cardName = card.Name.Current;

        return cardName.Equals("Mesmeric Control", StringComparison.OrdinalIgnoreCase)
               || cardName.Equals("Mesmeric Controller", StringComparison.OrdinalIgnoreCase)
               || cardName.Equals("Tears of the Mermaid", StringComparison.OrdinalIgnoreCase)
               || cardName.Equals("Paralyzing Potion", StringComparison.OrdinalIgnoreCase)
               || cardName.Equals("Invisible Wire", StringComparison.OrdinalIgnoreCase);
    }

    bool IsEquipmentCard(CardConstant card)
    {
        return card.Index >= Card.EquipCardStartIndex && card.Index <= Card.EquipCardEndIndex;
    }

    string GetDraftCardType(CardConstant card)
    {
        if (cardTypeOverrides.TryGetValue(card.Index, out string? draftType))
        {
            return draftType;
        }

        return card.Type ?? "Unknown";
    }

    float GetRarityWeight(DraftRarity rarity)
    {
        switch (rarity)
        {
            case DraftRarity.Common:
                return settings.CommonWeight;
            case DraftRarity.Uncommon:
                return settings.UncommonWeight;
            case DraftRarity.Rare:
                return settings.RareWeight;
            case DraftRarity.SuperRare:
                return settings.SuperRareWeight;
            default:
                return 1f;
        }
    }

    float GetRarityScoreWeight(float rarityScore)
    {
        rarityScore = Math.Clamp(rarityScore, 0f, 100f);

        if (rarityScore <= 33f)
        {
            return Lerp(settings.CommonWeight, settings.UncommonWeight, rarityScore / 33f);
        }

        if (rarityScore <= 66f)
        {
            return Lerp(settings.UncommonWeight, settings.RareWeight, (rarityScore - 33f) / 33f);
        }

        return Lerp(settings.RareWeight, settings.SuperRareWeight, (rarityScore - 66f) / 34f);
    }

    float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * Math.Clamp(amount, 0f, 1f);
    }

    float ScoreWithVariation(float baseScore, ushort cardIndex, float variationPercent)
    {
        int hash = (cardIndex * 1103515245 + 12345) & 0x7fffffff;
        float variation = (hash % 2001) / 1000f - 1f;
        float variedScore = baseScore + baseScore * variationPercent * variation;
        return Math.Clamp(variedScore, 1f, 99f);
    }

    void ClampSettings()
    {
        settings.SameRarityOfferChance = Math.Clamp(settings.SameRarityOfferChance, 0f, 1f);
        settings.SimilarRarityScoreOfferChance = Math.Clamp(settings.SimilarRarityScoreOfferChance, 0f, 1f);
        settings.SimilarRarityScoreRange = Math.Clamp(settings.SimilarRarityScoreRange, 1f, 50f);
        settings.SameTypeOfferChance = Math.Clamp(settings.SameTypeOfferChance, 0f, 1f);
        settings.CommonWeight = Math.Max(0f, settings.CommonWeight);
        settings.UncommonWeight = Math.Max(0f, settings.UncommonWeight);
        settings.RareWeight = Math.Max(0f, settings.RareWeight);
        settings.SuperRareWeight = Math.Max(0f, settings.SuperRareWeight);
    }

    List<CardConstant> DraftableCards
    {
        get
        {
            return CardConstant.List
                .Where(IsDraftableCard)
                .ToList();
        }
    }

    bool IsDraftableCard(CardConstant card)
    {
        return !BannedCardIndices.Contains(card.Index);
    }

    HashSet<ushort> BannedCardIndices
    {
        get
        {
            HashSet<ushort> bannedCardIndices = new HashSet<ushort>(BaseBannedCardIndices);

            foreach (CardConstant card in CardConstant.List)
            {
                if (!card.CardKind.isMonster())
                {
                    continue;
                }

                if (card.Attack > 2400 || card.CardKind.Id == (byte)CardKind.CardKindEnum.Immortal)
                {
                    bannedCardIndices.Add(card.Index);
                }
            }

            return bannedCardIndices;
        }
    }

    bool CanFinishDeckAfterPicking(CardConstant card)
    {
        int cardsLeftAfterPick = 40 - DraftDeck.CardList.Count - 1;
        int deckCostAfterPick = DraftDeck.DeckCost + card.DeckCost;

        if (deckCostAfterPick > MaxDraftDeckCost)
        {
            return false;
        }

        int cheapestCardCost = DraftableCards.Min(cardConstant => cardConstant.DeckCost);
        return deckCostAfterPick + cardsLeftAfterPick * cheapestCardCost <= MaxDraftDeckCost;
    }

    int MaxDraftDeckCost
    {
        get { return BudgetDeckIndices.Min(deckIndex => Deck.DeckList[deckIndex].DeckCost) - 1; }
    }

    int RemainingDraftBudget
    {
        get { return MaxDraftDeckCost - DraftDeck.DeckCost; }
    }

    float RemainingAverageDeckCost
    {
        get
        {
            int cardsLeft = 40 - DraftDeck.CardList.Count;
            if (cardsLeft <= 0)
            {
                return 0;
            }

            return RemainingDraftBudget / (float)cardsLeft;
        }
    }

    float TargetUsedDeckCost
    {
        get { return MaxDraftDeckCost * (DraftDeck.CardList.Count / 40f); }
    }

    float BudgetPressure
    {
        get { return DraftDeck.DeckCost - TargetUsedDeckCost; }
    }

    BudgetStatus CurrentBudgetStatus
    {
        get
        {
            if (BudgetPressure <= 30)
            {
                return BudgetStatus.Good;
            }

            if (BudgetPressure <= 80)
            {
                return BudgetStatus.Tight;
            }

            return BudgetStatus.Critical;
        }
    }

    bool IsDraftDeckUnderBudget
    {
        get { return DraftDeck.DeckCost <= MaxDraftDeckCost; }
    }
}
