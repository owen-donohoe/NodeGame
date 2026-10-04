using System.Collections.Generic;
using NodeWar.Lobby;

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
        public static void TapVillager(List<int> selected, int villagerId, InputAction action)
        {
            if (selected.Remove(villagerId)) return;
            if (action == InputAction.Replace) selected.Clear();
            selected.Add(villagerId);
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
