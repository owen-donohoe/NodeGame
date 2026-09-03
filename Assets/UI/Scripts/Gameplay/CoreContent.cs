using System.Collections.Generic;
using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Core. Breach pressure, and the roster of dead villagers waiting to come
    /// back - each with a button that pays to skip the wait.
    ///
    /// Respawn is the one command with no district check at all:
    /// ProcessRespawnCommand puts the villager at the player's own coreNodeID
    /// wherever the command was issued from. It is surfaced here by convention
    /// rather than because the simulation requires it, which is worth knowing
    /// before anyone moves it.
    ///
    /// AN ENEMY CORE SHOWS BREACH AND NOTHING ELSE. The casualty roster is
    /// information the opponent never chose to reveal, and every button on it
    /// would be dead anyway - ProcessRespawnCommand rejects a villager whose
    /// ownerID is not the issuing player. The uGUI CorePanelContent made the
    /// same call for the same reason.
    /// </summary>
    public class CoreContent : NodeSheetContent
    {
        private Label breachLabel;
        private Label costLabel;
        private VisualElement rosterHost;
        private Label emptyLabel;

        private readonly List<RespawnRow> rows = new List<RespawnRow>();

        protected override void OnBind()
        {
            Root.Clear();
            rows.Clear();

            breachLabel = Body("Breaches 0 / 0");
            breachLabel.AddToClassList("sheet__headline");
            Root.Add(breachLabel);

            Root.Add(Caption("A villager that breaches is spent for good - it does not come back."));

            costLabel = Caption("");
            Root.Add(costLabel);

            rosterHost = new VisualElement();
            rosterHost.AddToClassList("core__roster");
            rosterHost.pickingMode = PickingMode.Ignore;
            Root.Add(rosterHost);

            emptyLabel = Caption("Everyone is alive.");
            Root.Add(emptyLabel);
        }

        public override void Refresh()
        {
            int coreOwner = State.nodes[NodeID].ownerID;
            int breaches = State.players[coreOwner].breachCount;

            breachLabel.text = "Breaches " + breaches + " / " + Balance.breachThreshold;

            bool owned = coreOwner == ControlledPID;

            if (!owned)
            {
                rosterHost.AddToClassList("sheet__hidden");
                costLabel.AddToClassList("sheet__hidden");
                emptyLabel.text = "Enemy core. Breach pressure is public; their casualties are not.";
                emptyLabel.RemoveFromClassList("sheet__hidden");
                return;
            }

            costLabel.RemoveFromClassList("sheet__hidden");
            rosterHost.RemoveFromClassList("sheet__hidden");

            int cost = RespawnCost(coreOwner);
            int food = State.players[coreOwner].food;

            costLabel.text = "Respawn costs " + cost + " food. You have " + food + ".";

            RefreshRoster(coreOwner, cost, food);
        }

        /// <summary>
        /// The same arithmetic ProcessRespawnCommand does, so the price shown is
        /// the price charged. Sanctuary workers discount it by a percentage
        /// each, and the floor is 1 - integer division throughout, matching the
        /// simulation exactly rather than approximately.
        /// </summary>
        private int RespawnCost(int playerID)
        {
            int baseCost = Balance.respawnCostFood;
            int workers = CountSanctuaryWorkers(playerID);
            int reduction = (baseCost * (Balance.sanctuaryRespawnCostReductionPercent * workers)) / 100;

            int cost = baseCost - reduction;
            return cost < 1 ? 1 : cost;
        }

        private int CountSanctuaryWorkers(int playerID)
        {
            int count = 0;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;

                int node = v.currentNodeID;
                if (node < 0 || node >= State.nodes.Length) continue;
                if (State.nodes[node].districtType != DistrictType.Sanctuary) continue;

                count++;
            }

            return count;
        }

        private void RefreshRoster(int coreOwner, int cost, int food)
        {
            int shown = 0;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.ownerID != coreOwner) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                RespawnRow row = RowAt(shown);
                row.Set(i, v, cost, food, Balance.respawnTicks,
                        Ticks != null ? Ticks.TickAlpha : 0f);
                shown++;
            }

            for (int i = shown; i < rows.Count; i++)
                rows[i].Hide();

            emptyLabel.EnableInClassList("sheet__hidden", shown > 0);
        }

        /// <summary>
        /// Rows are pooled rather than rebuilt. The roster changes every time
        /// anything dies or returns, and rebuilding the subtree each frame would
        /// destroy the element a finger is currently on.
        /// </summary>
        private RespawnRow RowAt(int index)
        {
            while (rows.Count <= index)
            {
                RespawnRow created = new RespawnRow(OnRespawnPressed);
                rows.Add(created);
                rosterHost.Add(created.Root);
            }

            return rows[index];
        }

        private void OnRespawnPressed(int villagerID)
        {
            Send(new GameCommand
            {
                type = CommandType.Respawn,
                playerID = ControlledPID,
                villagerID = villagerID
            });
        }

        /// <summary>
        /// One dead villager: how long it has left, and the button to pay the
        /// wait away. Disabled when the food is not there, with the shortfall
        /// said out loud rather than left as a button that does nothing.
        /// </summary>
        private class RespawnRow
        {
            public VisualElement Root { get; private set; }

            private readonly Label label;
            private readonly VisualElement fill;
            private readonly Button button;
            private readonly System.Action<int> onPressed;

            private int villagerID = -1;

            public RespawnRow(System.Action<int> pressed)
            {
                onPressed = pressed;

                Root = new VisualElement();
                Root.AddToClassList("core__row");
                Root.pickingMode = PickingMode.Ignore;

                VisualElement text = new VisualElement();
                text.AddToClassList("core__row-text");
                text.pickingMode = PickingMode.Ignore;

                label = new Label();
                label.AddToClassList("caption");
                label.pickingMode = PickingMode.Ignore;

                VisualElement track = new VisualElement();
                track.AddToClassList("core__track");
                track.pickingMode = PickingMode.Ignore;

                fill = new VisualElement();
                fill.AddToClassList("core__track-fill");
                fill.pickingMode = PickingMode.Ignore;

                track.Add(fill);
                text.Add(label);
                text.Add(track);

                button = new Button(() => { if (villagerID >= 0) onPressed(villagerID); });
                button.AddToClassList("sheet__action");
                button.text = "Respawn";

                Root.Add(text);
                Root.Add(button);
            }

            public void Set(int id, VillagerData villager, int cost, int food,
                            int respawnTicks, float tickAlpha)
            {
                villagerID = id;
                Root.RemoveFromClassList("sheet__hidden");

                int remaining = villager.respawnTicksRemaining;
                label.text = "Villager " + id + " - " + remaining + " ticks left";

                // How much of the wait is done, taken from GameBalanceData
                // rather than a literal. RespawnEntryDisplay uses its own
                // RESPAWN_TICKS constant, which is the same number written twice
                // and only correct until someone retunes the balance asset.
                //
                // TickAlpha adds the fraction of the way to the next tick, so
                // the bar moves every frame over a 10Hz simulation.
                float fraction = 1f;

                if (respawnTicks > 0 && remaining > 0)
                {
                    fraction = 1f - (remaining / (float)respawnTicks);
                    fraction += (1f / respawnTicks) * tickAlpha;
                }

                if (fraction < 0f) fraction = 0f;
                if (fraction > 1f) fraction = 1f;

                fill.style.width = Length.Percent(fraction * 100f);

                bool affordable = food >= cost;
                button.SetEnabled(affordable);
                button.text = affordable ? "Respawn" : "Need " + (cost - food) + " food";
            }

            public void Hide()
            {
                villagerID = -1;
                Root.AddToClassList("sheet__hidden");
            }
        }
    }
}
