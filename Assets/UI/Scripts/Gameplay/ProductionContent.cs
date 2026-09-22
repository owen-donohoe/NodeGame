using UnityEngine.UIElements;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Farm, Mine and Market. DistrictPanelPolicy.HasSheet only opens this for
    /// the node's own owner, so unlike Forge and Core there is no enemy view to
    /// account for -- the sheet either shows your own production or, if the
    /// node changed hands while it was open, says so and stops.
    ///
    /// No action bar: these districts take no command, so Actions stays empty.
    ///
    /// Shares its dial-per-worker shape with ForgeContent, but the label under
    /// each dial and the caption's resource line depend on the district, and a
    /// single instance of this class is reused across all three districts (see
    /// NodeSheet.ContentFor) -- so both are read from SimulationState in
    /// Refresh rather than baked in OnBind.
    /// </summary>
    public class ProductionContent : NodeSheetContent
    {
        private VisualElement gauges;
        private ProgressDial[] dials;
        private Label[] dialTexts;
        private Label[] dialLabels;
        private VisualElement[] dialGauges;
        private int[] shownPercentages;

        private Label idleCaption;
        private Label slotsLabel;
        private VisualElement workingCaption;
        private Label notYoursCaption;

        private DistrictType shownDistrict = (DistrictType)(-1);
        private int shownSlots = int.MinValue;
        private int shownWorking = -1;

        protected override int LayoutKey { get { return Balance.maxWorkersPerNode; } }

        // A few lines and at most two dials, and nothing to press.
        public override bool Compact { get { return true; } }

        /// <summary>
        /// Farm, Mine and Market share this content, so which resources it is
        /// about depends on the node it is currently bound to rather than
        /// being fixed per class.
        /// </summary>
        public override ResourceKind InvolvedResources
        {
            get
            {
                switch (State.nodes[NodeID].districtType)
                {
                    case DistrictType.Farm: return ResourceKind.Food;
                    case DistrictType.Mine: return ResourceKind.Materials;
                    case DistrictType.Market: return ResourceKind.Food | ResourceKind.Materials;
                    default: return ResourceKind.None;
                }
            }
        }

        protected override void OnBind()
        {
            int cap = Balance.maxWorkersPerNode > 0 ? Balance.maxWorkersPerNode : 2;

            gauges = Box("production__gauges");
            dials = new ProgressDial[cap];
            dialTexts = new Label[cap];
            dialLabels = new Label[cap];
            dialGauges = new VisualElement[cap];
            shownPercentages = new int[cap];

            for (int i = 0; i < cap; i++)
            {
                shownPercentages[i] = -1;
                VisualElement gauge = Gauge(out dials[i], out dialTexts[i], out dialLabels[i]);
                dialGauges[i] = gauge;
                gauges.Add(gauge);
            }
            Root.Add(gauges);

            idleCaption = Caption("No one is working here.");
            slotsLabel = Text("", "ui-w600");
            workingCaption = ResourceRow("sheet__caption");
            notYoursCaption = Caption("This node is no longer yours.");

            Root.Add(idleCaption);
            Root.Add(slotsLabel);
            Root.Add(workingCaption);
            Root.Add(notYoursCaption);

            shownDistrict = (DistrictType)(-1);
            shownSlots = int.MinValue;
            shownWorking = -1;
        }

        private static VisualElement Gauge(out ProgressDial dial, out Label text, out Label label)
        {
            VisualElement gauge = Box("production__gauge");

            dial = new ProgressDial();
            dial.AddToClassList("production__dial");

            text = Text("0%", "production__dial-text", "ui-w600");
            dial.Add(text);

            gauge.Add(dial);
            label = Text("", "production__gauge-label", "ui-w600");
            gauge.Add(label);
            return gauge;
        }

        public override void Refresh()
        {
            NodeData node = State.nodes[NodeID];
            bool yours = node.ownerID == ControlledPID;

            Show(notYoursCaption, !yours);

            if (!yours)
            {
                Show(gauges, false);
                Show(idleCaption, false);
                Show(slotsLabel, false);
                Show(workingCaption, false);
                return;
            }

            if (shownDistrict != node.districtType)
            {
                shownDistrict = node.districtType;

                // One content serves every farm, mine and market, so a new
                // district is a new caption even at the same worker count.
                shownWorking = -1;
                shownSlots = int.MinValue;
                string role = RoleFor(node.districtType);
                for (int i = 0; i < dialLabels.Length; i++) dialLabels[i].text = role;
            }

            int working = RefreshDials(node.ownerID);
            int cap = dials.Length;

            Show(gauges, working > 0);
            Show(idleCaption, working == 0);
            Show(slotsLabel, working == 0);
            Show(workingCaption, working > 0);

            if (working == 0)
            {
                int slots = cap - working;
                if (shownSlots != slots)
                {
                    shownSlots = slots;
                    slotsLabel.text = slots + (slots == 1 ? " villager can work here" : " villagers can work here");
                }
                return;
            }

            if (shownWorking != working)
            {
                shownWorking = working;
                SetResourceText(workingCaption, working + "/" + cap + " working · " + CycleFor(node.districtType));
            }
        }

        /// <summary>
        /// One dial per working villager of the node's owner, the same loop
        /// ForgeContent.RefreshDials uses.
        /// </summary>
        private int RefreshDials(int nodeOwner)
        {
            int cap = dials.Length;
            int found = 0;

            for (int i = 0; i < State.villagers.Length && found < cap; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.currentNodeID != NodeID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (v.ownerID != nodeOwner) continue;

                float progress = Progress(v);
                dials[found].Progress = progress;
                int percentage = (int)(progress * 100f);
                if (shownPercentages[found] != percentage)
                {
                    shownPercentages[found] = percentage;
                    dialTexts[found].text = percentage + "%";
                }
                Show(dialGauges[found], true);
                found++;
            }

            for (int i = found; i < cap; i++)
                Show(dialGauges[i], false);

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

        private static string RoleFor(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm: return "Farmer";
                case DistrictType.Mine: return "Miner";
                case DistrictType.Market: return "Merchant";
                default: return "Worker";
            }
        }

        private static string CycleFor(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm: return "+1 {food} each cycle";
                case DistrictType.Mine: return "+1 {materials} each cycle";
                case DistrictType.Market: return "alternates {food} and {materials}";
                default: return "";
            }
        }
    }
}
