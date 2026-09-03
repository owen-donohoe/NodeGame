using System.Collections.Generic;
using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// The equip bench, shared by Barracks, Camp, Arsenal and Sanctuary - the
    /// four districts CanEquipSuitAtNode accepts. They differ only in which
    /// suits they permit, and that difference is data, so one content covers
    /// all four rather than four near-identical ones.
    ///
    /// EVERY REFUSAL IS NAMED. ProcessEquipCommand drops a command it will not
    /// run without a word, and there are seven separate ways it can refuse:
    /// wrong owner, dead, consumed, not idle, already wearing a combat suit,
    /// suit not drafted, or not enough food and materials. A bench that just
    /// greys a button out leaves the player guessing which one applies, so each
    /// villager row says.
    /// </summary>
    public class EquipContent : NodeSheetContent
    {
        private Label districtLabel;
        private VisualElement rosterHost;
        private Label emptyLabel;

        private readonly List<VillagerRow> rows = new List<VillagerRow>();
        private readonly List<SuitType> permittedSuits = new List<SuitType>();

        private int selectedVillager = -1;

        protected override void OnBind()
        {
            Root.Clear();
            rows.Clear();
            selectedVillager = -1;

            CollectPermittedSuits();

            districtLabel = Caption("");
            Root.Add(districtLabel);

            rosterHost = new VisualElement();
            rosterHost.AddToClassList("equip__roster");
            rosterHost.pickingMode = PickingMode.Ignore;
            Root.Add(rosterHost);

            emptyLabel = Caption("");
            Root.Add(emptyLabel);
        }

        /// <summary>
        /// Which suits this district accepts, asked of GameBalanceData rather
        /// than restated. Iterating the enum means a new SuitType shows up here
        /// the moment CanEquipSuitAtNode admits it.
        /// </summary>
        private void CollectPermittedSuits()
        {
            permittedSuits.Clear();

            DistrictType district = State.nodes[NodeID].districtType;
            System.Array all = System.Enum.GetValues(typeof(SuitType));

            for (int i = 0; i < all.Length; i++)
            {
                SuitType suit = (SuitType)all.GetValue(i);

                if (!GameBalanceData.IsCombatSuit(suit)) continue;
                if (!Balance.CanEquipSuitAtNode(suit, district)) continue;

                permittedSuits.Add(suit);
            }
        }

        public override void Refresh()
        {
            bool owned = State.nodes[NodeID].ownerID == ControlledPID;

            districtLabel.text = "Fits here: " + DescribeSuits();

            if (!owned)
            {
                rosterHost.AddToClassList("sheet__hidden");
                emptyLabel.RemoveFromClassList("sheet__hidden");
                emptyLabel.text = "Not your district. You cannot equip here.";
                return;
            }

            rosterHost.RemoveFromClassList("sheet__hidden");

            int shown = 0;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.ownerID != ControlledPID) continue;
                if (v.currentNodeID != NodeID) continue;
                if (v.isConsumed) continue;
                if (v.state == VillagerState.Dead) continue;

                VillagerRow row = RowAt(shown);
                row.Set(i, v, this);
                shown++;
            }

            for (int i = shown; i < rows.Count; i++)
                rows[i].Hide();

            emptyLabel.EnableInClassList("sheet__hidden", shown > 0);

            if (shown == 0)
                emptyLabel.text = "No villagers of yours are standing here.";
        }

        private string DescribeSuits()
        {
            if (permittedSuits.Count == 0) return "nothing";

            string result = "";

            for (int i = 0; i < permittedSuits.Count; i++)
                result += (result.Length > 0 ? ", " : "") + permittedSuits[i];

            return result;
        }

        private VillagerRow RowAt(int index)
        {
            while (rows.Count <= index)
            {
                VillagerRow created = new VillagerRow(OnVillagerSelected, OnSuitChosen);
                rows.Add(created);
                rosterHost.Add(created.Root);
            }

            return rows[index];
        }

        private void OnVillagerSelected(int villagerID)
        {
            selectedVillager = selectedVillager == villagerID ? -1 : villagerID;
            Refresh();
        }

        private void OnSuitChosen(int villagerID, SuitType suit)
        {
            Send(new GameCommand
            {
                type = CommandType.Equip,
                playerID = ControlledPID,
                villagerID = villagerID,
                value = (int)suit
            });

            selectedVillager = -1;
        }

        /// <summary>
        /// Why ProcessEquipCommand would refuse this villager, in its own order
        /// of checks, or null when it would accept. Kept as one method so the
        /// reason shown and the reason the simulation acts on cannot drift into
        /// disagreeing.
        /// </summary>
        private string RefusalFor(VillagerData villager)
        {
            if (villager.state != VillagerState.Idle)
                return "Busy - " + villager.state.ToString().ToLowerInvariant();

            if (GameBalanceData.IsCombatSuit(villager.suit))
                return "Already wearing " + villager.suit;

            if (permittedSuits.Count == 0)
                return "This district fits no suits";

            return null;
        }

        /// <summary>
        /// Whether this suit could actually be put on, and what stops it. Drafted
        /// state and cost are both checked, because both are refusals the player
        /// can do something about - one in the Workshop, one by waiting.
        /// </summary>
        private string SuitRefusal(SuitType suit)
        {
            if (!PlayerHasDrafted(suit))
                return "not drafted";

            SuitStats stats = Balance.GetSuitStats(suit);
            PlayerData player = State.players[ControlledPID];

            if (player.food < stats.foodCost || player.materials < stats.materialCost)
                return stats.foodCost + "f " + stats.materialCost + "m";

            return null;
        }

        private bool PlayerHasDrafted(SuitType suit)
        {
            int[] drafted = State.players[ControlledPID].draftedSuits;
            if (drafted == null) return false;

            for (int i = 0; i < drafted.Length; i++)
            {
                if (drafted[i] == (int)suit) return true;
            }

            return false;
        }

        /// <summary>
        /// One villager standing at this district. Tapping it opens the suit
        /// choices underneath, so the bench does not show every suit for every
        /// villager at once on a phone-width sheet.
        /// </summary>
        private class VillagerRow
        {
            public VisualElement Root { get; private set; }

            private readonly Button header;
            private readonly Label title;
            private readonly Label subtitle;
            private readonly VisualElement suitHost;
            private readonly System.Action<int> onSelected;
            private readonly System.Action<int, SuitType> onSuitChosen;

            private int villagerID = -1;

            public VillagerRow(System.Action<int> selected, System.Action<int, SuitType> suitChosen)
            {
                onSelected = selected;
                onSuitChosen = suitChosen;

                Root = new VisualElement();
                Root.AddToClassList("equip__villager");
                Root.pickingMode = PickingMode.Ignore;

                header = new Button(() => { if (villagerID >= 0) onSelected(villagerID); });
                header.AddToClassList("equip__villager-header");

                VisualElement text = new VisualElement();
                text.AddToClassList("equip__villager-text");
                text.pickingMode = PickingMode.Ignore;

                title = new Label();
                title.AddToClassList("body");
                title.pickingMode = PickingMode.Ignore;

                subtitle = new Label();
                subtitle.AddToClassList("caption");
                subtitle.pickingMode = PickingMode.Ignore;

                text.Add(title);
                text.Add(subtitle);
                header.Add(text);

                suitHost = new VisualElement();
                suitHost.AddToClassList("equip__suits");
                suitHost.pickingMode = PickingMode.Ignore;

                Root.Add(header);
                Root.Add(suitHost);
            }

            public void Set(int id, VillagerData villager, EquipContent owner)
            {
                villagerID = id;
                Root.RemoveFromClassList("sheet__hidden");

                title.text = "Villager " + id;

                string refusal = owner.RefusalFor(villager);
                bool equippable = refusal == null;

                subtitle.text = equippable ? "Ready to equip" : refusal;
                header.SetEnabled(equippable);

                bool open = equippable && owner.selectedVillager == id;
                suitHost.EnableInClassList("sheet__hidden", !open);

                if (!open) return;

                BuildSuitButtons(owner);
            }

            private void BuildSuitButtons(EquipContent owner)
            {
                suitHost.Clear();

                for (int i = 0; i < owner.permittedSuits.Count; i++)
                {
                    SuitType suit = owner.permittedSuits[i];
                    string refusal = owner.SuitRefusal(suit);

                    Button button = new Button();
                    button.AddToClassList("sheet__action");
                    button.AddToClassList("equip__suit");
                    button.text = refusal == null ? suit.ToString() : suit + " - " + refusal;
                    button.SetEnabled(refusal == null);

                    if (refusal == null)
                    {
                        int captured = villagerID;
                        SuitType capturedSuit = suit;
                        button.clicked += () => onSuitChosen(captured, capturedSuit);
                    }

                    suitHost.Add(button);
                }
            }

            public void Hide()
            {
                villagerID = -1;
                Root.AddToClassList("sheet__hidden");
            }
        }
    }
}
