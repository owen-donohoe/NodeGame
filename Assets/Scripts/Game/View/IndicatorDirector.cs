using System.Collections.Generic;
using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>One thing an indicator is currently saying, and about what.</summary>
    public sealed class ActiveIndicator
    {
        /// <summary>Unique for the life of the match and never reused, so the UI can key an element on it.</summary>
        public int id;

        public IndicatorKind kind;

        /// <summary>The node it is about, or -1 when the subject is a villager.</summary>
        public int nodeID = -1;

        /// <summary>The villager it is about, or -1 when the subject is a node.</summary>
        public int villagerID = -1;

        /// <summary>Past its debounce and meant to be on screen.</summary>
        public bool shown;

        /// <summary>A node under attack that has just fallen. Shown louder, briefly, then gone.</summary>
        public bool lost;

        // Wall-clock bookkeeping, view side only.
        internal float firstSeen;
        internal float lastTrue;
        internal float expiresAt;
        internal bool held;
        internal bool seen;
    }

    /// <summary>
    /// Decides which indicators exist. Draws nothing: IndicatorLayer, in the UI
    /// Toolkit HUD, turns this list into elements.
    ///
    /// Two sources, split the way TickEventLog splits them. Moments -- a fight
    /// starting, a node falling -- arrive as tick events. Conditions -- a fight
    /// still going, a node still being pushed -- are read off SimulationState
    /// once per tick, because a condition is what the state already says.
    /// Everything runs from ITickProvider.TickSimulated, so a paused or ended
    /// match changes nothing here.
    ///
    /// Read-only over SimulationState, like every view. The timing it adds --
    /// how long before something shows, how long it lingers -- is wall-clock
    /// and presentation only.
    /// </summary>
    public sealed class IndicatorDirector
    {
        private readonly SimulationState state;
        private readonly IndicatorSettings settings;
        private readonly System.Func<int> localPlayer;
        private readonly NodeWar.Core.ITickProvider tickProvider;

        private NodeSlotManager[] nodeSlotManagers;
        private Transform[] villagerTransforms;

        private readonly List<ActiveIndicator> active = new List<ActiveIndicator>();
        private int nextID = 1;

        // Per-node tallies, rebuilt every tick.
        private int[] fighting;
        private int[] enemyClaimers;

        public IReadOnlyList<ActiveIndicator> Active => active;

        public IndicatorSettings Settings => settings;

        public IndicatorDirector(SimulationState state, NodeWar.Core.ITickProvider tickProvider,
                                 IndicatorSettings settings, System.Func<int> localPlayer)
        {
            this.state = state;
            this.tickProvider = tickProvider;
            this.settings = settings ?? new IndicatorSettings();
            this.localPlayer = localPlayer;

            if (tickProvider != null)
                tickProvider.TickSimulated += OnTickSimulated;
        }

        public void Dispose()
        {
            if (tickProvider != null)
                tickProvider.TickSimulated -= OnTickSimulated;

            active.Clear();
        }

        public void SetNodeSlotManagers(NodeSlotManager[] managers)
        {
            nodeSlotManagers = managers;
        }

        /// <summary>Grows with the villager array, so GameManager hands it over again after a bonus spawn.</summary>
        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
        }

        /// <summary>
        /// Where an indicator's subject is. world is where the icon floats;
        /// ground is the spot the camera should look at when asked to go there.
        /// False when the subject has no view to anchor to.
        /// </summary>
        public bool TryGetAnchor(ActiveIndicator indicator, out Vector3 world, out Vector3 ground)
        {
            world = ground = Vector3.zero;

            if (indicator.villagerID >= 0)
            {
                if (villagerTransforms == null || indicator.villagerID >= villagerTransforms.Length) return false;

                Transform t = villagerTransforms[indicator.villagerID];
                if (t == null) return false;

                ground = t.position;
                world = ground + Vector3.up * settings.villagerHeight;
                return true;
            }

            if (indicator.nodeID >= 0)
            {
                if (nodeSlotManagers == null || indicator.nodeID >= nodeSlotManagers.Length) return false;

                NodeSlotManager node = nodeSlotManagers[indicator.nodeID];
                if (node == null) return false;

                ground = node.transform.position;
                world = ground + Vector3.up * settings.nodeHeight;
                return true;
            }

            return false;
        }

        // ===== PER TICK =====

        private void OnTickSimulated(TickEventLog log)
        {
            if (state == null || state.nodes == null || state.villagers == null) return;

            float now = Time.time;
            int me = localPlayer != null ? localPlayer() : 0;

            for (int i = 0; i < active.Count; i++)
                active[i].seen = false;

            if (log != null) ReadEvents(log, me, now);
            ReadConditions(me, now);
            Settle(now);
        }

        private void ReadEvents(TickEventLog log, int me, float now)
        {
            for (int i = 0; i < log.Count; i++)
            {
                TickEvent e = log[i];

                switch (e.type)
                {
                    case TickEventType.CombatStarted:
                        Touch(IndicatorKind.Battle, e.nodeID, -1, now);
                        break;

                    case TickEventType.NodeNeutralised:
                        if (e.playerID == me) MarkLost(e.nodeID, now);
                        break;
                }
            }
        }

        /// <summary>
        /// One pass over the villagers for the per-node tallies, then one over
        /// the nodes. Enemy means not the local player, so a local-test switch
        /// of sides re-points every condition on the next tick.
        /// </summary>
        private void ReadConditions(int me, float now)
        {
            int nodeCount = state.nodes.Length;

            if (fighting == null || fighting.Length < nodeCount)
            {
                fighting = new int[nodeCount];
                enemyClaimers = new int[nodeCount];
            }

            for (int n = 0; n < nodeCount; n++)
            {
                fighting[n] = 0;
                enemyClaimers[n] = 0;
            }

            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData villager = state.villagers[v];
                if (villager.isConsumed || villager.state == VillagerState.Dead) continue;

                int node = villager.currentNodeID;
                if (node < 0 || node >= nodeCount) continue;

                if (villager.state == VillagerState.Fighting) fighting[node]++;
                else if (villager.state == VillagerState.Claiming && villager.ownerID != me) enemyClaimers[node]++;
            }

            for (int n = 0; n < nodeCount; n++)
            {
                NodeData node = state.nodes[n];

                // A fight is only ever started by its event; the condition is
                // what keeps it on screen.
                if (fighting[n] > 0) KeepAlive(IndicatorKind.Battle, n, -1);

                if (enemyClaimers[n] == 0) continue;

                if (node.ownerID == me)
                {
                    Touch(IndicatorKind.NodeUnderAttack, n, -1, now);
                }
                else if (node.ownerID < 0 && LeansToward(node.claimBar, me))
                {
                    Touch(IndicatorKind.NodeContested, n, -1, now);
                }
            }
        }

        /// <summary>Positive claim is player 0's, negative player 1's.</summary>
        private static bool LeansToward(int claimBar, int playerID)
        {
            return playerID == 0 ? claimBar > 0 : claimBar < 0;
        }

        /// <summary>
        /// Moves every indicator on by one tick: shows the ones past their
        /// debounce, and drops the ones whose condition has been gone for longer
        /// than their grace, or whose hold has run out.
        /// </summary>
        private void Settle(float now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveIndicator a = active[i];
                IndicatorKindSettings rules = settings.For(a.kind);

                if (!rules.enabled)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (a.held)
                {
                    if (now >= a.expiresAt) active.RemoveAt(i);
                    continue;
                }

                if (a.seen)
                {
                    a.lastTrue = now;
                    if (!a.shown && now - a.firstSeen >= rules.minSeconds) a.shown = true;
                    continue;
                }

                if (now - a.lastTrue > rules.graceSeconds)
                    active.RemoveAt(i);
            }
        }

        // ===== BOOKKEEPING =====

        /// <summary>Creates the indicator if it is new, and marks it true this tick.</summary>
        private void Touch(IndicatorKind kind, int nodeID, int villagerID, float now)
        {
            if (!settings.For(kind).enabled) return;

            ActiveIndicator a = Find(kind, nodeID, villagerID);
            if (a == null)
            {
                a = new ActiveIndicator
                {
                    id = nextID++,
                    kind = kind,
                    nodeID = nodeID,
                    villagerID = villagerID,
                    firstSeen = now,
                    lastTrue = now
                };
                active.Add(a);
            }

            a.seen = true;
        }

        /// <summary>Marks an existing indicator true this tick without creating one.</summary>
        private void KeepAlive(IndicatorKind kind, int nodeID, int villagerID)
        {
            ActiveIndicator a = Find(kind, nodeID, villagerID);
            if (a != null) a.seen = true;
        }

        /// <summary>
        /// A node of yours was pushed to neutral. Whatever was saying it was
        /// under attack now says it fell, at once and past any debounce --
        /// losing a node is not something to hold back -- then clears itself.
        /// </summary>
        private void MarkLost(int nodeID, float now)
        {
            IndicatorKindSettings rules = settings.nodeUnderAttack;
            if (!rules.enabled) return;

            Touch(IndicatorKind.NodeUnderAttack, nodeID, -1, now);

            ActiveIndicator a = Find(IndicatorKind.NodeUnderAttack, nodeID, -1);
            if (a == null) return;

            a.lost = true;
            a.shown = true;
            a.held = true;
            a.expiresAt = now + rules.holdSeconds;
        }

        private ActiveIndicator Find(IndicatorKind kind, int nodeID, int villagerID)
        {
            for (int i = 0; i < active.Count; i++)
            {
                ActiveIndicator a = active[i];
                if (a.kind == kind && a.nodeID == nodeID && a.villagerID == villagerID) return a;
            }
            return null;
        }
    }
}
