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

    static readonly int[] BudgetDeckIndices = { 28, 29, 37 };
    readonly Random random = new Random();
    readonly CardConstant?[] currentChoices = new CardConstant?[3];
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

            for (int i = 0; i < 2; i++)
            {
                CardConstant? budgetCard = DrawWeightedCard(budgetFriendlyCards);
                if (budgetCard == null)
                {
                    break;
                }

                currentChoices[i] = budgetCard;
                availableCards.Remove(budgetCard);
            }
        }

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

            currentChoices[i] = DrawWeightedCard(availableCards);
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

        switch (CurrentBudgetStatus)
        {
            case BudgetStatus.Good:
                return 1;
            case BudgetStatus.Tight:
                return Math.Max(1, 25 - overAverage * 1.5f + underAverage * 0.25f);
            case BudgetStatus.Critical:
                return Math.Max(1, 35 - overAverage * 4f + underAverage * 0.75f);
            default:
                return 1;
        }
    }

    List<CardConstant> DraftableCards
    {
        get
        {
            return CardConstant.List
                .Where(card => card.Index != 671)
                .ToList();
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
