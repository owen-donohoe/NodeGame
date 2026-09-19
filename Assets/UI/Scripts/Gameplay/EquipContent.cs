using System.Collections.Generic;
using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// The equip bench, shared by Barracks, Camp, Arsenal and Sanctuary - the
    /// four districts CanEquipSuitAtNode accepts. They differ only in which
    /// suits they permit, and that difference is data, so one content covers
    /// all four.
    ///
    /// SUIT FIRST, THEN UNIT, THEN EQUIP, after the prototype. The suits that
    /// fit here sit in a row of cards; the villagers standing here sit below as
    /// chips. Pick one of each and the action bar's button equips.
    ///
    /// EVERY REFUSAL IS NAMED. ProcessEquipCommand drops a command it will not
    /// run without a word. A card says "not drafted", or shows the price when
    /// you are short; a chip says what the villager is busy doing or which suit
    /// it already wears; the button says what is still missing. Each answer
    /// comes from CommandEligibility, in CommandProcessor's own order.
    /// </summary>
    public class EquipContent : NodeSheetContent
    {
        private readonly List<SuitType> fits = new List<SuitType>();
        private readonly List<SuitCard> cards = new List<SuitCard>();
        private readonly List<UnitChip> chips = new List<UnitChip>();

        private Label fitsLine;
        private Label enemyNote;
        private VisualElement yourSide;
        private VisualElement unitHost;
        private Label emptyLabel;
        private Button equipButton;
        private (SuitType picked, EquipRefusal refusal, VillagerState state,
                 SuitType worn, int food, int materials)? shownEquip;

        private SuitType pickedSuit = SuitType.None;
        private int pickedUnit = -1;

        public override bool Tall { get { return true; } }

        protected override int LayoutKey { get { return (int)State.nodes[NodeID].districtType; } }

        protected override void OnBind()
        {
            shownEquip = null;
            cards.Clear();
            chips.Clear();
            pickedSuit = SuitType.None;
            pickedUnit = -1;

            CollectFits();

            fitsLine = Heading("FITS HERE · " + DescribeFits());
            Root.Add(fitsLine);

            enemyNote = Caption("Not your district. You cannot equip here.");
            Root.Add(enemyNote);

            yourSide = Box("equip__yours");

            ScrollView rail = new ScrollView(ScrollViewMode.Horizontal);
            rail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rail.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            rail.contentContainer.AddToClassList("equip__rail");

            for (int i = 0; i < fits.Count; i++)
            {
                SuitCard card = new SuitCard(fits[i], OnCardPressed);
                cards.Add(card);
                rail.Add(card.Root);
            }

            yourSide.Add(rail);
            yourSide.Add(Box("equip__gap"));
            yourSide.Add(Heading("UNITS HERE"));

            unitHost = Box("equip__units");
            yourSide.Add(unitHost);

            emptyLabel = Caption("No villagers of yours are standing here.");
            yourSide.Add(emptyLabel);

            Root.Add(yourSide);

            equipButton = PrimaryButton(OnEquipPressed);
            Actions.Add(equipButton);
        }

        /// <summary>
        /// Which suits this district accepts, asked of GameBalanceData rather
        /// than restated. Iterating the enum means a new SuitType shows up here
        /// the moment CanEquipSuitAtNode admits it.
        /// </summary>
        private void CollectFits()
        {
            fits.Clear();

            DistrictType district = State.nodes[NodeID].districtType;
            System.Array all = System.Enum.GetValues(typeof(SuitType));

            for (int i = 0; i < all.Length; i++)
            {
                SuitType suit = (SuitType)all.GetValue(i);

                if (!GameBalanceData.IsCombatSuit(suit)) continue;
                if (!Balance.CanEquipSuitAtNode(suit, district)) continue;

                fits.Add(suit);
            }
        }

        private string DescribeFits()
        {
            if (fits.Count == 0) return "nothing";

            string result = "";
            for (int i = 0; i < fits.Count; i++)
                result += (i > 0 ? ", " : "") + fits[i];

            return result;
        }

        public override void Refresh()
        {
            bool yours = State.nodes[NodeID].ownerID == ControlledPID;

            Show(enemyNote, !yours);
            Show(yourSide, yours);
            Show(equipButton, yours && fits.Count > 0);

            if (!yours) return;

            for (int i = 0; i < cards.Count; i++)
                cards[i].Set(CommandEligibility.EquipSuit(State, Balance, ControlledPID, cards[i].Suit),
                             Balance.GetSuitStats(cards[i].Suit), pickedSuit == cards[i].Suit);

            int shown = RefreshUnits();
            Show(emptyLabel, shown == 0);

            RefreshButton();
        }

        private int RefreshUnits()
        {
            int shown = 0;
            bool pickedStillHere = false;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                // The roster is your living villagers standing on this node.
                // Equip applies at the villager's currentNodeID, so that is
                // what "standing here" has to mean.
                if (v.ownerID != ControlledPID) continue;
                if (v.currentNodeID != NodeID) continue;
                if (v.isConsumed) continue;
                if (v.state == VillagerState.Dead) continue;

                EquipRefusal refusal = CommandEligibility.EquipVillager(State, ControlledPID, i);
                ChipAt(shown).Set(i, v, refusal, pickedUnit == i);
                if (pickedUnit == i) pickedStillHere = true;
                shown++;
            }

            for (int i = shown; i < chips.Count; i++)
                chips[i].Hide();

            if (!pickedStillHere) pickedUnit = -1;

            return shown;
        }

        private UnitChip ChipAt(int index)
        {
            while (chips.Count <= index)
            {
                UnitChip created = new UnitChip(OnUnitPressed);
                chips.Add(created);
                unitHost.Add(created.Root);
            }

            return chips[index];
        }

        /// <summary>
        /// The button says what is still missing, or what stands in the way,
        /// and is enabled only when the simulation would accept the command.
        /// </summary>
        private void RefreshButton()
        {
            if (pickedSuit == SuitType.None)
            {
                shownEquip = null;
                equipButton.text = "Pick a suit";
                equipButton.SetEnabled(false);
                return;
            }

            if (pickedUnit < 0)
            {
                shownEquip = null;
                equipButton.text = "Pick a unit";
                equipButton.SetEnabled(false);
                return;
            }

            EquipRefusal refusal = CommandEligibility.Equip(State, Balance, ControlledPID, pickedUnit, pickedSuit);
            equipButton.SetEnabled(refusal == EquipRefusal.None);
            VillagerData villager = State.villagers[pickedUnit];
            SuitStats stats = Balance.GetSuitStats(pickedSuit);
            var equipValue = (pickedSuit, refusal, villager.state, villager.suit,
                              stats.foodCost, stats.materialCost);
            if (shownEquip != equipValue)
            {
                shownEquip = equipValue;
                equipButton.text = refusal == EquipRefusal.None
                    ? "Equip " + pickedSuit
                    : RefusalText(refusal, villager, stats);
            }
        }

        private void OnCardPressed(SuitType suit)
        {
            pickedSuit = pickedSuit == suit ? SuitType.None : suit;
            Refresh();
        }

        private void OnUnitPressed(int villagerID)
        {
            pickedUnit = pickedUnit == villagerID ? -1 : villagerID;
            Refresh();
        }

        private void OnEquipPressed()
        {
            if (pickedSuit == SuitType.None || pickedUnit < 0) return;
            if (CommandEligibility.Equip(State, Balance, ControlledPID, pickedUnit, pickedSuit) != EquipRefusal.None) return;

            Send(new GameCommand
            {
                type = CommandType.Equip,
                playerID = ControlledPID,
                villagerID = pickedUnit,
                value = (int)pickedSuit
            });

            pickedSuit = SuitType.None;
            pickedUnit = -1;
        }

        private static string CostText(SuitStats stats)
        {
            if (stats.foodCost == 0 && stats.materialCost == 0) return "free";
            return stats.foodCost + "f " + stats.materialCost + "m";
        }

        private static string RefusalText(EquipRefusal refusal, VillagerData villager, SuitStats stats)
        {
            switch (refusal)
            {
                case EquipRefusal.Busy: return "Unit is " + villager.state.ToString().ToLowerInvariant();
                case EquipRefusal.AlreadySuited: return "Already wearing " + villager.suit;
                case EquipRefusal.NotDrafted: return "Not drafted";
                case EquipRefusal.CannotAfford: return "Need " + CostText(stats);
                case EquipRefusal.DoesNotFit: return "Does not fit here";
                case EquipRefusal.NodeNotYours: return "Not your district";
                default: return "Cannot equip";
            }
        }

        /// <summary>One suit that fits here: its tile, name, and price or refusal.</summary>
        private class SuitCard
        {
            public VisualElement Root { get; private set; }
            public SuitType Suit { get; private set; }

            private readonly Label sub;
            private (bool notDrafted, int food, int materials)? shownCost;

            public SuitCard(SuitType suit, System.Action<SuitType> pressed)
            {
                Suit = suit;

                Button button = new Button(() => pressed(suit));
                button.AddToClassList("ui-reset-button");
                button.AddToClassList("equip__card");
                Root = button;

                string name = suit.ToString();

                VisualElement tile = Box("ui-tile", "equip__card-tile", ItemTint.ClassFor("suit_" + name.ToLowerInvariant()));
                tile.Add(Text(name.Substring(0, 1), "ui-tile__monogram", "equip__card-letter"));

                sub = Text("", "equip__card-sub");

                button.Add(tile);
                button.Add(Text(name, "equip__card-name", "ui-w600"));
                button.Add(sub);
            }

            public void Set(EquipRefusal refusal, SuitStats stats, bool picked)
            {
                bool notDrafted = refusal == EquipRefusal.NotDrafted;

                // Always the price, unless it is not yours to buy at all. Short
                // is dimmed and the price turns the short-ink colour - never red,
                // which on this surface means player 2.
                var costValue = (notDrafted, stats.foodCost, stats.materialCost);
                if (shownCost != costValue)
                {
                    shownCost = costValue;
                    sub.text = notDrafted ? "not drafted" : CostText(stats);
                }
                sub.EnableInClassList("equip__card-sub--short", refusal == EquipRefusal.CannotAfford);

                Root.EnableInClassList("equip__card--dim", refusal != EquipRefusal.None);
                Root.EnableInClassList("equip__card--picked", picked);
            }
        }

        /// <summary>One of your villagers standing here, and whether it can take a suit.</summary>
        private class UnitChip
        {
            public VisualElement Root { get; private set; }

            private readonly Button button;
            private readonly Label name;
            private readonly Label state;
            private int villagerID = -1;
            private int shownID = -1;
            private (EquipRefusal refusal, VillagerState state, SuitType suit)? shownState;

            public UnitChip(System.Action<int> pressed)
            {
                button = new Button(() => { if (villagerID >= 0) pressed(villagerID); });
                button.AddToClassList("ui-reset-button");
                button.AddToClassList("equip__unit");
                Root = button;

                name = Text("", "equip__unit-name", "ui-w600");
                state = Text("", "equip__unit-state");
                button.Add(name);
                button.Add(state);
            }

            public void Set(int id, VillagerData villager, EquipRefusal refusal, bool picked)
            {
                villagerID = id;
                Show(Root, true);

                if (shownID != id)
                {
                    shownID = id;
                    name.text = "Villager " + id;
                }

                var stateValue = (refusal, villager.state, villager.suit);
                if (shownState != stateValue)
                {
                    shownState = stateValue;
                    if (refusal == EquipRefusal.AlreadySuited) state.text = "has " + villager.suit;
                    else if (refusal == EquipRefusal.Busy) state.text = villager.state.ToString().ToLowerInvariant();
                    else state.text = "idle";
                }

                button.SetEnabled(refusal == EquipRefusal.None);
                Root.EnableInClassList("equip__unit--picked", picked);
            }

            public void Hide()
            {
                villagerID = -1;
                Show(Root, false);
            }
        }
    }
}
