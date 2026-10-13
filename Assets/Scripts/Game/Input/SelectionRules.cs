using System.Collections.Generic;
using NodeWar.Lobby;
using NodeWar.Simulation;

namespace NodeWar.Input
{
    public readonly struct VillagerPickCandidate
    {
        public readonly int Id;
        public readonly GesturePoint Position;
        public readonly bool Selectable;
        public readonly bool InFront;
        public VillagerPickCandidate(int id, GesturePoint position, bool selectable, bool inFront)
        {
            Id = id; Position = position; Selectable = selectable; InFront = inFront;
        }
    }

    /// <summary>Selection edits after the Unity owner has checked that the target is selectable.</summary>
    public static class SelectionRules
    {
        public static bool IsValidNode(SimulationState state, int node) =>
            state != null && state.nodes != null && node >= 0 && node < state.nodes.Length;

        private static bool IsResident(VillagerData villager, int player, int node) =>
            villager.ownerID == player && !villager.isConsumed &&
            villager.state != VillagerState.Dead && villager.state != VillagerState.Moving &&
            villager.currentNodeID == node;

        public static void CollectOnNode(SimulationState state, int player, int node, bool idleOnly, List<int> result)
        {
            result.Clear();
            if (!IsValidNode(state, node) || state.villagers == null) return;
            // Simulation villager IDs are their array indices; traversal gives ascending IDs.
            for (int id = 0; id < state.villagers.Length; id++)
                if (IsResident(state.villagers[id], player, node) &&
                    (!idleOnly || state.villagers[id].state == VillagerState.Idle)) result.Add(id);
        }

        public static bool AllResidentsSelected(SimulationState state, int player, int node, IReadOnlyList<int> selected)
        {
            if (!IsValidNode(state, node) || state.villagers == null) return false;
            bool any = false;
            for (int id = 0; id < state.villagers.Length; id++)
            {
                if (!IsResident(state.villagers[id], player, node)) continue;
                any = true;
                bool found = false;
                for (int i = 0; i < selected.Count; i++)
                    if (selected[i] == id) { found = true; break; }
                if (!found) return false;
            }
            return any;
        }

        public static void TapVillager(List<int> selected, int villagerId, InputAction action)
        {
            if (selected.Remove(villagerId)) return;
            if (action == InputAction.Replace) selected.Clear();
            selected.Add(villagerId);
        }

        public static void ReplaceLassoIfAny(List<int> selected, IReadOnlyList<int> captured)
        {
            if (captured.Count == 0) return;
            selected.Clear();
            for (int i = 0; i < captured.Count; i++) selected.Add(captured[i]);
        }

        public static int NearestVillager(IReadOnlyList<VillagerPickCandidate> candidates, GesturePoint pointer)
        {
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                VillagerPickCandidate candidate = candidates[i];
                if (!candidate.Selectable || !candidate.InFront || candidate.Id < 0) continue;
                float x = candidate.Position.X - pointer.X, y = candidate.Position.Y - pointer.Y;
                float distance = x * x + y * y;
                if (float.IsNaN(distance) || float.IsInfinity(distance)) continue;
                if (distance < best || (distance == best && candidate.Id < nearest))
                {
                    nearest = candidate.Id;
                    best = distance;
                }
            }
            return nearest;
        }
    }
}
