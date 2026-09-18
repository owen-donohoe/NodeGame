using System.Collections.Generic;
using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Core. Breach pressure, the roster of dead villagers waiting to come
    /// back, and one button that pays to skip a wait.
    ///
    /// ONE BUTTON, LONGEST WAIT. A respawned villager returns with base stats
    /// and no suit, so the dead differ only in how long they have left. Paying
    /// to skip the longest wait is always the best use of the food, so the
    /// sheet offers that rather than a button per row (settled 2026-09-15). The
    /// roster still shows every timer, and marks the one the button brings back.
    ///
    /// Respawn is the one command with no district check at all:
    /// ProcessRespawnCommand puts the villager at the player's own core
    /// wherever the command was issued from. It is surfaced here by convention
    /// rather than because the simulation requires it.
    ///
    /// AN ENEMY CORE SHOWS BREACH AND NOTHING ELSE. The casualty roster is
    /// information the opponent never chose to reveal, and every button on it
    /// would be refused anyway.
    /// </summary>
    public class CoreContent : NodeSheetContent
    {
        private Label statValue;
        private Label costLine;
        private Label enemyNote;
        private VisualElement roster;
        private Label emptyLabel;
        private Button respawnButton;
        private (int breaches, int threshold)? shownBreach;
        private (int cost, int food)? shownCost;
        private (RespawnRefusal refusal, int cost, int food)? shownRespawn;

        private readonly List<RespawnRow> rows = new List<RespawnRow>();

        public override bool Tall { get { return true; } }

        protected override void OnBind()
        {
            rows.Clear();
            shownBreach = null;
            shownCost = null;
            shownRespawn = null;

            VisualElement cols = Box("core__cols");

            VisualElement left = Box("core__left");
            VisualElement stat = Box("core__stat");
            statValue = Text("0/3", "core__stat-value", "ui-w600");
            stat.Add(statValue);
            stat.Add(Text("BREACHES", "core__stat-label", "ui-w600"));
            left.Add(stat);
            left.Add(Text("A villager that breaches is spent for good.", "sheet__microcap"));

            VisualElement right = Box("core__right");
            costLine = Caption("");
            enemyNote = Caption("Enemy core. Breach pressure is public; their casualties are not.");
            roster = Box("core__roster");
            emptyLabel = Caption("Everyone is alive.");
            right.Add(costLine);
            right.Add(enemyNote);
            right.Add(roster);
            right.Add(emptyLabel);

            cols.Add(left);
            cols.Add(right);
            Root.Add(cols);

            respawnButton = PrimaryButton(OnRespawnPressed);
            Actions.Add(respawnButton);
        }

        /// <summary>
        /// Whose core this is. A core belongs to the player whose coreNodeID it
        /// is, which stays true while the node's claim is being fought over.
        /// </summary>
        private int CorePlayer()
        {
            for (int i = 0; i < State.players.Length; i++)
            {
                if (State.players[i].coreNodeID == NodeID) return i;
            }

            return State.nodes[NodeID].ownerID;
        }

        public override void Refresh()
        {
            int corePlayer = CorePlayer();
            int breaches = corePlayer >= 0 ? State.players[corePlayer].breachCount : 0;

            var breachValue = (breaches, Balance.breachThreshold);
            if (shownBreach != breachValue)
            {
                shownBreach = breachValue;
                statValue.text = breaches + "/" + Balance.breachThreshold;
            }

            bool yours = corePlayer == ControlledPID;

            Show(enemyNote, !yours);
            Show(costLine, yours);
            Show(roster, yours);

            if (!yours)
            {
                Show(emptyLabel, false);
                Show(respawnButton, false);
                return;
            }

            int cost = CommandEligibility.RespawnCost(State, Balance, ControlledPID);
            int food = State.players[ControlledPID].food;
            var costValue = (cost, food);
            if (shownCost != costValue)
            {
                shownCost = costValue;
                costLine.text = "Respawn costs " + cost + " food. You have " + food + ".";
            }

            int next = CommandEligibility.RespawnTarget(State, ControlledPID);
            int shown = RefreshRoster(next);

            Show(emptyLabel, shown == 0);
            Show(respawnButton, next >= 0);

            if (next < 0) return;

            RespawnRefusal refusal = CommandEligibility.Respawn(State, Balance, ControlledPID, next);
            respawnButton.SetEnabled(refusal == RespawnRefusal.None);
            var respawnValue = (refusal, cost, food);
            if (shownRespawn != respawnValue)
            {
                shownRespawn = respawnValue;
                respawnButton.text = refusal == RespawnRefusal.CannotAfford
                    ? "Need " + (cost - food) + " more food"
                    : "Respawn longest wait · " + cost + " food";
            }
        }

        private int RefreshRoster(int next)
        {
            int shown = 0;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.ownerID != ControlledPID) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                RowAt(shown).Set(i, v, i == next, Balance, Ticks != null ? Ticks.TickAlpha : 0f);
                shown++;
            }

            for (int i = shown; i < rows.Count; i++)
                Show(rows[i].Root, false);

            return shown;
        }

        /// <summary>
        /// Rows are pooled across sheet opens. The roster changes every time
        /// anything dies or returns, and rebuilding the subtree each frame would
        /// churn elements for nothing.
        /// </summary>
        private RespawnRow RowAt(int index)
        {
            while (rows.Count <= index)
            {
                RespawnRow created = new RespawnRow();
                rows.Add(created);
                roster.Add(created.Root);
            }

            return rows[index];
        }

        /// <summary>
        /// Asks again at the moment of the press, rather than trusting what the
        /// button showed a frame ago: the longest wait may have changed.
        /// </summary>
        private void OnRespawnPressed()
        {
            int target = CommandEligibility.RespawnTarget(State, ControlledPID);
            if (target < 0) return;
            if (CommandEligibility.Respawn(State, Balance, ControlledPID, target) != RespawnRefusal.None) return;

            Send(new GameCommand
            {
                type = CommandType.Respawn,
                playerID = ControlledPID,
                villagerID = target
            });
        }

        /// <summary>One dead villager: how long it has left, as time and as a bar.</summary>
        private class RespawnRow
        {
            public VisualElement Root { get; private set; }

            private readonly Label name;
            private readonly Label time;
            private readonly Label nextTag;
            private readonly VisualElement fill;
            private int shownID = -1;
            private (int remaining, int ticksPerSecond)? shownTime;

            public RespawnRow()
            {
                Root = Box("core__row");

                VisualElement text = Box("core__row-text");
                name = Text("", "core__row-name", "ui-w600");
                time = Text("", "core__row-time");
                text.Add(name);
                text.Add(time);

                nextTag = Text("NEXT", "core__row-next", "ui-w700");

                VisualElement track = Box("core__track");
                fill = Box("core__track-fill");
                track.Add(fill);

                Root.Add(text);
                Root.Add(nextTag);
                Root.Add(track);
            }

            public void Set(int id, VillagerData villager, bool isNext, GameBalanceData balance, float tickAlpha)
            {
                Show(Root, true);
                Show(nextTag, isNext);

                if (shownID != id)
                {
                    shownID = id;
                    name.text = "Villager " + id;
                }

                int remaining = villager.respawnTicksRemaining;
                int ticksPerSecond = balance.ticksPerSecond > 0 ? balance.ticksPerSecond : 10;
                var timeValue = (remaining, ticksPerSecond);
                if (shownTime != timeValue)
                {
                    shownTime = timeValue;
                    time.text = (remaining / (float)ticksPerSecond).ToString("0.0") + "s left";
                }

                // How much of the wait is done, from the balance rather than a
                // literal - the uGUI roster hardcoded 50 ticks. TickAlpha adds
                // the fraction of the way to the next tick, so the bar glides
                // over a 10Hz simulation.
                float fraction = 1f;
                int respawnTicks = balance.respawnTicks;

                if (respawnTicks > 0 && remaining > 0)
                    fraction = 1f - (remaining - tickAlpha) / respawnTicks;

                if (fraction < 0f) fraction = 0f;
                if (fraction > 1f) fraction = 1f;

                fill.style.width = Length.Percent(fraction * 100f);
            }
        }
    }
}
