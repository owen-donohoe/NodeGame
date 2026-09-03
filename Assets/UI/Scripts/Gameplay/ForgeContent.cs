using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Forge. The only district SetAllocation accepts - CommandProcessor
    /// rejects the command outright for any other district type - so this is
    /// the only content that issues one.
    ///
    /// Shows the two production slots and how far each worker is through its
    /// cycle, plus the material allocation and a pair of controls to change it.
    ///
    /// Allocation is unbounded upward in the simulation: ProcessSetAllocation
    /// refuses a negative value and accepts anything else. The buttons stop at
    /// zero for that reason and nothing else - there is no maximum to clamp to,
    /// so inventing one here would be a rule this UI made up.
    /// </summary>
    public class ForgeContent : NodeSheetContent
    {
        private const int MaxProductionSlots = 2;

        private Label workerLabel;
        private readonly VisualElement[] slotFills = new VisualElement[MaxProductionSlots];
        private readonly VisualElement[] slotRoots = new VisualElement[MaxProductionSlots];

        private VisualElement allocationSection;
        private Label allocationValue;
        private Button decreaseButton;
        private Button increaseButton;
        private Label notYours;

        protected override void OnBind()
        {
            Root.Clear();

            workerLabel = Caption("Workers 0 / " + MaxProductionSlots);
            Root.Add(workerLabel);

            VisualElement slots = new VisualElement();
            slots.AddToClassList("forge__slots");
            slots.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < MaxProductionSlots; i++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("forge__slot");
                slot.pickingMode = PickingMode.Ignore;

                VisualElement fill = new VisualElement();
                fill.AddToClassList("forge__slot-fill");
                fill.pickingMode = PickingMode.Ignore;

                slot.Add(fill);
                slots.Add(slot);

                slotRoots[i] = slot;
                slotFills[i] = fill;
            }

            Root.Add(slots);

            BuildAllocation();

            notYours = Reason("This forge belongs to your opponent. You can see what it is doing, not change it.");
            Root.Add(notYours);
        }

        private void BuildAllocation()
        {
            allocationSection = new VisualElement();
            allocationSection.AddToClassList("forge__allocation");
            allocationSection.pickingMode = PickingMode.Ignore;

            allocationSection.Add(Caption("Materials allocated"));

            VisualElement row = new VisualElement();
            row.AddToClassList("forge__allocation-row");
            row.pickingMode = PickingMode.Ignore;

            decreaseButton = new Button(() => ChangeAllocation(-1));
            decreaseButton.AddToClassList("sheet__step");
            decreaseButton.text = "-";

            allocationValue = new Label("0");
            allocationValue.AddToClassList("forge__allocation-value");
            allocationValue.pickingMode = PickingMode.Ignore;

            increaseButton = new Button(() => ChangeAllocation(1));
            increaseButton.AddToClassList("sheet__step");
            increaseButton.text = "+";

            row.Add(decreaseButton);
            row.Add(allocationValue);
            row.Add(increaseButton);

            allocationSection.Add(row);
            Root.Add(allocationSection);
        }

        private void ChangeAllocation(int delta)
        {
            int current = State.nodes[NodeID].materialAllocation;
            int next = current + delta;

            // Mirrors ProcessSetAllocation's only bound. Sending a negative
            // would be dropped in silence, which is exactly the shape of bug
            // this sheet exists to avoid.
            if (next < 0) return;

            Send(new GameCommand
            {
                type = CommandType.SetAllocation,
                playerID = ControlledPID,
                targetNodeID = NodeID,
                value = next
            });
        }

        public override void Refresh()
        {
            bool owned = State.nodes[NodeID].ownerID == ControlledPID;

            allocationSection.EnableInClassList("sheet__hidden", !owned);
            notYours.EnableInClassList("sheet__hidden", owned);
            workerLabel.EnableInClassList("sheet__hidden", !owned);

            RefreshSlots();

            if (!owned) return;

            int allocation = State.nodes[NodeID].materialAllocation;
            allocationValue.text = allocation.ToString();
            decreaseButton.SetEnabled(allocation > 0);
        }

        /// <summary>
        /// One bar per working villager, filled by how far through its
        /// production cycle it is. TickAlpha carries the fraction of the way to
        /// the next tick, so the bar advances smoothly at 60fps over a
        /// simulation that only moves at 10Hz - the same trick the uGUI disks
        /// use, and the reason this reads as motion rather than a stutter.
        /// </summary>
        private void RefreshSlots()
        {
            int nodeOwner = State.nodes[NodeID].ownerID;
            int found = 0;

            for (int i = 0; i < State.villagers.Length && found < MaxProductionSlots; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.currentNodeID != NodeID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (v.ownerID != nodeOwner) continue;

                slotRoots[found].RemoveFromClassList("sheet__hidden");
                slotFills[found].style.width = Length.Percent(ProgressPercent(v));

                found++;
            }

            for (int i = found; i < MaxProductionSlots; i++)
                slotRoots[i].AddToClassList("sheet__hidden");

            workerLabel.text = "Workers " + found + " / " + MaxProductionSlots;
        }

        private float ProgressPercent(VillagerData villager)
        {
            if (villager.productionTicksMax <= 0) return 0f;

            float done = 1f - (villager.productionTicksRemaining / (float)villager.productionTicksMax);
            float subTick = (1f / villager.productionTicksMax) * (Ticks != null ? Ticks.TickAlpha : 0f);

            float total = done + subTick;
            if (total < 0f) total = 0f;
            if (total > 1f) total = 1f;

            return total * 100f;
        }
    }
}
