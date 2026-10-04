using System.Collections.Generic;
using NodeWar.Lobby;

namespace NodeWar.Input
{
    /// <summary>Selection edits after the Unity owner has checked that the target is selectable.</summary>
    public static class SelectionRules
    {
        public static void TapVillager(List<int> selected, int villagerId, InputAction action)
        {
            if (selected.Remove(villagerId)) return;
            if (action == InputAction.Replace) selected.Clear();
            selected.Add(villagerId);
        }
    }
}
