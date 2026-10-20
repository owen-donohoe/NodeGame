namespace NodeWar.Lobby
{
    public enum DistrictClusterState { Rest, Selected, Replacing }
    public enum DistrictClusterAction { None, FillEmpty, Replaced }

    public readonly struct DistrictClusterResult
    {
        public DistrictClusterAction Action { get; }
        public int Slot { get; }

        public DistrictClusterResult(DistrictClusterAction action, int slot = -1)
        {
            Action = action;
            Slot = slot;
        }
    }

    public sealed class DistrictCluster
    {
        private readonly StageSlot[] slots;

        public DistrictClusterState State { get; private set; }
        public int SelectedSlot { get; private set; } = -1;
        public bool Shaking => State == DistrictClusterState.Replacing;

        public DistrictCluster(int equipped)
        {
            slots = StageSlots.Layout(equipped);
        }

        public int TargetSeparation(int slot)
        {
            if (!Valid(slot)) return 0;
            return Shaking || SelectedSlot == slot ? 1 : 0;
        }

        public void Select(int slot)
        {
            if (Shaking || !Valid(slot) || !slots[slot].Filled) return;
            State = DistrictClusterState.Selected;
            SelectedSlot = slot;
        }

        public void Deselect()
        {
            if (State == DistrictClusterState.Selected) Reset();
        }

        public DistrictClusterResult Use(bool hasEmptySlot)
        {
            if (Shaking) return default;
            Reset();
            if (hasEmptySlot) return new DistrictClusterResult(DistrictClusterAction.FillEmpty);
            State = DistrictClusterState.Replacing;
            return default;
        }

        public DistrictClusterResult Pick(int slot)
        {
            if (!Shaking || !Valid(slot) || !slots[slot].Filled) return default;
            Reset();
            return new DistrictClusterResult(DistrictClusterAction.Replaced, slot);
        }

        public void Cancel() => Reset();

        private static bool Valid(int slot) => slot >= 0 && slot < StageSlots.Count;

        private void Reset()
        {
            State = DistrictClusterState.Rest;
            SelectedSlot = -1;
        }
    }
}
