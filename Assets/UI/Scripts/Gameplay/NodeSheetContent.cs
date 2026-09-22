using System.Collections.Generic;
using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// One district's worth of node-sheet content.
    ///
    /// There are four of these and there should stay four. DistrictPanelPolicy.
    /// HasSheet already settled which districts open a sheet at all - the six
    /// with something to press, for anyone, plus Farm, Mine and Market for
    /// their own owner - and the rest are informational, with their state
    /// shown on the node itself. So Forge, Core, Equip and Production cover
    /// every case: Equip is shared by Barracks, Camp, Arsenal and Sanctuary
    /// because those four differ only in which suits they permit, and
    /// Production is shared by Farm, Mine and Market because those three
    /// differ only in what a working villager there produces.
    ///
    /// Each content fills two places: the sheet's scrolling body (Root) and its
    /// action bar, which holds the content's one main control and stays put
    /// above the home indicator while the body scrolls.
    ///
    /// A plain class, not a MonoBehaviour, like LobbyPage: content is elements
    /// in a panel, not objects in a scene.
    ///
    /// THE BOUNDARY. Content reads SimulationState and never writes it. The only
    /// way any of these changes the game is by enqueuing a GameCommand on the
    /// InputBuffer, which is the one path the rules allow. Nothing here calls
    /// GameSimulation or CommandProcessor - whether a command would be accepted
    /// is asked of CommandEligibility, which is tested against CommandProcessor.
    /// </summary>
    public abstract class NodeSheetContent
    {
        public VisualElement Root { get; private set; }

        private readonly List<VisualElement> actionElements = new List<VisualElement>();
        private SimulationState builtState;
        private int builtLayoutKey;

        // Core and Forge share a layout across nodes; Equip depends on the district.
        protected virtual int LayoutKey { get { return 0; } }

        protected SimulationState State { get; private set; }
        protected InputBuffer Input { get; private set; }
        protected NodeWar.Core.ITickProvider Ticks { get; private set; }
        protected GameBalanceData Balance { get; private set; }

        /// <summary>The sheet's action bar. Build the main control into it in OnBind.</summary>
        protected VisualElement Actions { get; private set; }

        /// <summary>The node this content is showing.</summary>
        protected int NodeID { get; private set; }

        /// <summary>The player whose side of the board we are looking from.</summary>
        protected int ControlledPID { get; private set; }

        /// <summary>Whether this content wants the taller sheet.</summary>
        public virtual bool Tall { get { return false; } }

        /// <summary>
        /// Whether this content wants the short sheet: a reading with no
        /// actions, which should cover as little of the board as it can.
        /// </summary>
        public virtual bool Compact { get { return false; } }

        protected NodeSheetContent()
        {
            Root = new VisualElement();
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Bind(SimulationState state, InputBuffer input,
                         NodeWar.Core.ITickProvider ticks, GameBalanceData balance,
                         int nodeID, int controlledPID, VisualElement actions)
        {
            State = state;
            Input = input;
            Ticks = ticks;
            Balance = balance;
            NodeID = nodeID;
            ControlledPID = controlledPID;
            Actions = actions;

            int layoutKey = LayoutKey;
            if (builtState != state || builtLayoutKey != layoutKey)
            {
                Root.Clear();
                Actions.Clear();
                actionElements.Clear();
                OnBind();
                for (int i = 0; i < Actions.childCount; i++)
                    actionElements.Add(Actions[i]);
                builtState = state;
                builtLayoutKey = layoutKey;
            }
            else
            {
                // Switching content detaches controls without discarding their callbacks.
                for (int i = 0; i < actionElements.Count; i++)
                {
                    if (actionElements[i].parent != Actions) Actions.Add(actionElements[i]);
                }
            }
            Refresh();
        }

        /// <summary>The debug switch can change the viewer while the sheet is open.</summary>
        public void SetViewer(int controlledPID)
        {
            ControlledPID = controlledPID;
        }

        /// <summary>Called when the content layout changes. Build here.</summary>
        protected virtual void OnBind() { }

        /// <summary>Called every frame while the sheet is open. Read state here.</summary>
        public abstract void Refresh();

        /// <summary>
        /// Sends a command the only legal way. Every command carries the tick it
        /// was issued on, because lockstep needs to agree on when it happened,
        /// not just what it was.
        /// </summary>
        protected void Send(GameCommand command)
        {
            if (Input == null) return;

            command.issuedOnTick = State.tickCount;
            Input.EnqueueCommand(command);
        }

        // ===== SHARED HELPERS =====

        protected static void Show(VisualElement element, bool visible)
        {
            if (element != null) element.EnableInClassList("sheet__hidden", !visible);
        }

        protected static Label Text(string text, params string[] classes)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < classes.Length; i++) label.AddToClassList(classes[i]);
            return label;
        }

        protected static Label Caption(string text)
        {
            return Text(text, "sheet__caption");
        }

        /// <summary>A small upper-case heading over a group, e.g. "FITS HERE".</summary>
        protected static Label Heading(string text)
        {
            return Text(text, "sheet__label", "ui-w600");
        }

        protected static VisualElement Box(params string[] classes)
        {
            VisualElement element = new VisualElement();
            element.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < classes.Length; i++) element.AddToClassList(classes[i]);
            return element;
        }

        /// <summary>The sheet's main control, on the shared gold button.</summary>
        protected static Button PrimaryButton(System.Action onClick)
        {
            Button button = new Button(onClick);
            button.AddToClassList("ui-button");
            button.AddToClassList("ui-button--gold");
            button.AddToClassList("sheet__primary");
            button.AddToClassList("ui-w600");
            return button;
        }
    }
}
