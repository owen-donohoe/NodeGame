using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Forge. The only district SetAllocation accepts, so the only content that
    /// issues one.
    ///
    /// Shows a dial per working smelter - how far through its cycle it is - and
    /// a dial for workers against the cap, then the allocation stepper in the
    /// action bar. What a forge is doing is public, so an enemy forge shows its
    /// dials too; only the stepper is withheld.
    ///
    /// Allocation is unbounded upward in the simulation: ProcessSetAllocation
    /// refuses a negative value and accepts anything else. The minus button
    /// stops at zero for that reason and nothing else - there is no maximum to
    /// clamp to, so inventing one here would be a rule this UI made up.
    /// </summary>
    public class ForgeContent : NodeSheetContent
    {
        private VisualElement gauges;
        private ProgressDial[] smelterDials;
        private Label[] smelterTexts;
        private VisualElement[] smelterGauges;
        private ProgressDial workerDial;
        private Label workerText;
        private Label idleNote;
        private Label explain;
        private Label enemyNote;

        private VisualElement stepper;
        private Button decrease;
        private Button increase;
        private Label allocationValue;

        protected override void OnBind()
        {
            int cap = Balance.maxWorkersPerNode > 0 ? Balance.maxWorkersPerNode : 2;

            gauges = Box("forge__gauges");
            smelterDials = new ProgressDial[cap];
            smelterTexts = new Label[cap];
            smelterGauges = new VisualElement[cap];

            for (int i = 0; i < cap; i++)
            {
                VisualElement gauge = Gauge(out smelterDials[i], out smelterTexts[i], "Smelter " + (i + 1));
                smelterGauges[i] = gauge;
                gauges.Add(gauge);
            }

            gauges.Add(Gauge(out workerDial, out workerText, "Workers"));
            Root.Add(gauges);

            idleNote = Caption("No smelter is working here.");
            explain = Text("The forge turns up to this many materials into metal, one each cycle.", "sheet__microcap");
            enemyNote = Caption("This forge belongs to your opponent. You can see what it is doing, not change it.");
            Root.Add(idleNote);
            Root.Add(explain);
            Root.Add(enemyNote);

            BuildStepper();
        }

        private static VisualElement Gauge(out ProgressDial dial, out Label text, string label)
        {
            VisualElement gauge = Box("forge__gauge");

            dial = new ProgressDial();
            dial.AddToClassList("forge__dial");

            text = Text("0%", "forge__dial-text", "ui-w600");
            dial.Add(text);

            gauge.Add(dial);
            gauge.Add(Text(label, "forge__gauge-label", "ui-w600"));
            return gauge;
        }

        private void BuildStepper()
        {
            stepper = Box("forge__stepper");

            decrease = new Button(() => ChangeAllocation(-1));
            decrease.AddToClassList("forge__step");
            decrease.AddToClassList("ui-w600");
            decrease.text = "-";

            VisualElement value = Box("forge__step-value");
            allocationValue = Text("0", "forge__step-number", "ui-w600");
            value.Add(allocationValue);
            value.Add(Text("MATERIALS ALLOCATED", "forge__step-label", "ui-w600"));

            increase = new Button(() => ChangeAllocation(1));
            increase.AddToClassList("forge__step");
            increase.AddToClassList("ui-w600");
            increase.text = "+";

            stepper.Add(decrease);
            stepper.Add(value);
            stepper.Add(increase);
            Actions.Add(stepper);
        }

        private void ChangeAllocation(int delta)
        {
            int next = State.nodes[NodeID].materialAllocation + delta;

            // Asked, not assumed: a command the simulation would drop is never
            // sent, because a press that silently does nothing looks like a bug.
            if (CommandEligibility.Allocation(State, ControlledPID, NodeID, next) != AllocationRefusal.None) return;

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
            bool yours = State.nodes[NodeID].ownerID == ControlledPID;

            int working = RefreshDials();

            Show(idleNote, working == 0);
            Show(explain, yours);
            Show(enemyNote, !yours);
            Show(stepper, yours);

            if (!yours) return;

            int allocation = State.nodes[NodeID].materialAllocation;
            allocationValue.text = allocation.ToString();
            decrease.SetEnabled(CommandEligibility.Allocation(State, ControlledPID, NodeID, allocation - 1) == AllocationRefusal.None);
        }

        /// <summary>
        /// One dial per working villager of the node's owner, filled by how far
        /// through its production cycle it is. TickAlpha carries the fraction of
        /// the way to the next tick, so the ring moves smoothly over a
        /// simulation that only steps at 10Hz. Only as many dials as are working.
        /// </summary>
        private int RefreshDials()
        {
            int nodeOwner = State.nodes[NodeID].ownerID;
            int cap = smelterDials.Length;
            int found = 0;

            for (int i = 0; i < State.villagers.Length && found < cap; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.currentNodeID != NodeID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (v.ownerID != nodeOwner) continue;

                float progress = Progress(v);
                smelterDials[found].Progress = progress;
                smelterTexts[found].text = (int)(progress * 100f) + "%";
                Show(smelterGauges[found], true);
                found++;
            }

            for (int i = found; i < cap; i++)
                Show(smelterGauges[i], false);

            workerDial.Progress = found / (float)cap;
            workerText.text = found + "/" + cap;

            return found;
        }

        private float Progress(VillagerData villager)
        {
            if (villager.productionTicksMax <= 0) return 0f;

            float alpha = Ticks != null ? Ticks.TickAlpha : 0f;
            float done = 1f - (villager.productionTicksRemaining - alpha) / villager.productionTicksMax;

            if (done < 0f) return 0f;
            return done > 1f ? 1f : done;
        }
    }
}
