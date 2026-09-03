using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// The node panel, as a bottom sheet. The UI Toolkit counterpart to
    /// NodePanelManager's sliding panel.
    ///
    /// A SHEET, NOT A TAKEOVER. Settled 2026-09-02: the opponent keeps acting
    /// while this is open, so the board must stay visible behind it. It covers
    /// the bottom of the screen and no more, and a scrim would defeat the point.
    ///
    /// IT DOES NOT DECIDE WHEN TO OPEN. NodePanelManager already owns that -
    /// TapRouter arbitrates taps, DistrictPanelPolicy decides which districts
    /// are worth a sheet, and the camera focus session hangs off the same call.
    /// Duplicating any of that would give the game two things racing to answer
    /// one tap. So the old manager keeps the input path and, when suppressed,
    /// tells this what to show instead of showing it itself.
    ///
    /// Three contents cover all six functional districts. See NodeSheetContent.
    /// </summary>
    public class NodeSheet
    {
        public VisualElement Root { get; private set; }

        private readonly Label districtLabel;
        private readonly Label ownerLabel;
        private readonly VisualElement ownerMark;
        private readonly Label ownerMarkLabel;
        private readonly VisualElement claimP0;
        private readonly VisualElement claimP1;
        private readonly Label claimLabel;
        private readonly VisualElement contentHost;

        private readonly ForgeContent forge = new ForgeContent();
        private readonly CoreContent core = new CoreContent();
        private readonly EquipContent equip = new EquipContent();

        private SimulationState state;
        private InputBuffer input;
        private NodeWar.Core.ITickProvider ticks;
        private GameBalanceData balance;

        private NodeSheetContent current;
        private int nodeID = -1;

        public bool IsOpen { get { return nodeID >= 0; } }

        /// <summary>Raised when the player closes the sheet from its own button.</summary>
        public event System.Action Closed;

        public NodeSheet(VisualTreeAsset layout)
        {
            Root = new VisualElement();
            Root.name = "node-sheet-host";

            if (layout != null) layout.CloneTree(Root);

            districtLabel = Root.Q<Label>("sheet-district");
            ownerLabel = Root.Q<Label>("sheet-owner");
            ownerMark = Root.Q<VisualElement>("sheet-owner-mark");
            ownerMarkLabel = Root.Q<Label>("sheet-owner-mark-label");
            claimP0 = Root.Q<VisualElement>("sheet-claim-p0");
            claimP1 = Root.Q<VisualElement>("sheet-claim-p1");
            claimLabel = Root.Q<Label>("sheet-claim-label");
            contentHost = Root.Q<VisualElement>("sheet-content");

            Button close = Root.Q<Button>("sheet-close");
            if (close != null) close.clicked += () => { Close(); if (Closed != null) Closed(); };

            Close();
        }

        public void Bind(SimulationState simulationState, InputBuffer inputBuffer,
                         NodeWar.Core.ITickProvider tickProvider, GameBalanceData balanceData)
        {
            state = simulationState;
            input = inputBuffer;
            ticks = tickProvider;
            balance = balanceData;
        }

        /// <summary>
        /// Shows the sheet for a node. Called by the controller when
        /// NodePanelManager decides a panel should open, so every guard that
        /// manager applies - unclaimed nodes, informational districts - has
        /// already run.
        /// </summary>
        public void Open(int node, int controlledPID)
        {
            if (state == null) return;
            if (node < 0 || node >= state.nodes.Length) return;

            NodeSheetContent next = ContentFor(state.nodes[node].districtType);

            if (next == null)
            {
                // DistrictPanelPolicy should have stopped this, so reaching here
                // means the two disagree. Closing is the safe answer; an empty
                // sheet would say a district has actions when it has none.
                Close();
                return;
            }

            nodeID = node;

            if (current != next)
            {
                contentHost.Clear();
                contentHost.Add(next.Root);
                current = next;
            }

            current.Bind(state, input, ticks, balance, node, controlledPID);

            Root.RemoveFromClassList("sheet--hidden");

            RefreshHeader(controlledPID);
        }

        public void Close()
        {
            nodeID = -1;
            Root.AddToClassList("sheet--hidden");
        }

        /// <summary>
        /// Per-frame while open. The simulation moves under this at 10Hz and
        /// nothing announces a change, so the sheet re-reads rather than
        /// subscribing.
        /// </summary>
        public void Update(int controlledPID)
        {
            if (!IsOpen || state == null) return;

            // The node can change hands while the sheet is open, and a captured
            // district is a different panel - not a redecorated one.
            RefreshHeader(controlledPID);

            if (current != null) current.Refresh();
        }

        private NodeSheetContent ContentFor(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Forge:
                    return forge;

                case DistrictType.Core:
                    return core;

                // The four CanEquipSuitAtNode accepts. They differ only in which
                // suits they permit, which EquipContent asks GameBalanceData.
                case DistrictType.Barracks:
                case DistrictType.Camp:
                case DistrictType.Arsenal:
                case DistrictType.Sanctuary:
                    return equip;

                default:
                    return null;
            }
        }

        private void RefreshHeader(int controlledPID)
        {
            NodeData node = state.nodes[nodeID];

            if (districtLabel != null)
                districtLabel.text = node.districtType.ToString();

            RefreshOwner(node, controlledPID);
            RefreshClaim(node);
        }

        private void RefreshOwner(NodeData node, int controlledPID)
        {
            bool owned = node.ownerID == controlledPID;

            if (ownerLabel != null)
            {
                if (node.ownerID < 0) ownerLabel.text = "Unclaimed";
                else ownerLabel.text = owned ? "Yours" : "Theirs";
            }

            if (ownerMark == null) return;

            // Colour plus shape, as everywhere else: player 0 is a blue square
            // and player 1 a red circle, and the mark carries a number too.
            ownerMark.EnableInClassList("player-mark--p0", node.ownerID == 0);
            ownerMark.EnableInClassList("player-mark--p1", node.ownerID == 1);
            ownerMark.EnableInClassList("sheet__mark--none", node.ownerID < 0);

            if (ownerMarkLabel != null)
                ownerMarkLabel.text = node.ownerID < 0 ? "-" : (node.ownerID + 1).ToString();
        }

        /// <summary>
        /// The claim bar, drawn as a tug of war rather than a progress bar.
        ///
        /// claimBar is a signed number: positive is player 0, negative is player
        /// 1, and zero is neutral - the value crosses through nothing on its way
        /// from one owner to the other. A one-directional fill would say a node
        /// is "80% claimed" without saying by whom, and would show a node being
        /// pulled back toward neutral as progress.
        ///
        /// So both halves grow outward from a fixed centre line, and only one
        /// has width at a time.
        /// </summary>
        private void RefreshClaim(NodeData node)
        {
            if (claimP0 == null || claimP1 == null) return;

            // GameBalanceData is a struct, so there is no null to check for -
            // an unbound sheet has a zeroed one, and the threshold guard below
            // catches that case as well as a genuinely misconfigured asset.
            int threshold = balance.claimThreshold;

            if (threshold <= 0)
            {
                claimP0.style.width = Length.Percent(0f);
                claimP1.style.width = Length.Percent(0f);
                return;
            }

            int claim = node.claimBar;

            float toward0 = claim > 0 ? claim / (float)threshold : 0f;
            float toward1 = claim < 0 ? -claim / (float)threshold : 0f;

            if (toward0 > 1f) toward0 = 1f;
            if (toward1 > 1f) toward1 = 1f;

            claimP0.style.width = Length.Percent(toward0 * 100f);
            claimP1.style.width = Length.Percent(toward1 * 100f);

            if (claimLabel == null) return;

            if (claim == 0) claimLabel.text = "Neutral";
            else if (claim > 0) claimLabel.text = "Player 1  " + claim + " / " + threshold;
            else claimLabel.text = "Player 2  " + (-claim) + " / " + threshold;
        }
    }
}
