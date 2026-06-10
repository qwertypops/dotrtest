using System.Numerics;
using ImGuiNET;

namespace DotrModdingTool2IMGUI;

public class DraftingWindow : IImGuiWindow
{
    readonly Random random = new Random();
    readonly CardConstant[] currentChoices = new CardConstant[3];
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
        ImGui.Text($"Total DC {DraftDeck.DeckCost}");
        ImGui.SameLine();
        if (ImGui.Button("Reset draft"))
        {
            DraftDeck.CardList.Clear();
            saveStatus = "";
            DrawNewChoices();
        }
        ImGui.SameLine();
        if (DraftDeck.CardList.Count == 40)
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
        if (currentChoices.Any(choice => choice == null))
        {
            DrawNewChoices();
        }
    }

    void DrawNewChoices()
    {
        List<CardConstant> availableCards = CardConstant.List
            .Where(card => card.Index != 671)
            .ToList();

        for (int i = 0; i < currentChoices.Length; i++)
        {
            int randomIndex = random.Next(availableCards.Count);
            currentChoices[i] = availableCards[randomIndex];
            availableCards.RemoveAt(randomIndex);
        }
    }

    void DrawChoice(int choiceIndex, float height)
    {
        CardConstant card = currentChoices[choiceIndex];
        Vector2 choiceSize = new Vector2(ImGui.GetContentRegionAvail().X / (3 - choiceIndex), height);

        ImGui.PushID(choiceIndex);
        ImGui.BeginChild("DraftChoice", choiceSize, ImGuiChildFlags.Border | ImGuiChildFlags.AlwaysAutoResize);

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
            DrawNewChoices();
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
}
