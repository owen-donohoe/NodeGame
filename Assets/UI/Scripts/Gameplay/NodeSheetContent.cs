using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// One district's worth of node-sheet content.
    ///
    /// There are three of these and there should stay three. DistrictPanelPolicy
    /// already settled which districts open a sheet at all - only the six with
    /// something to press - and the twelve that do not are informational, with
    /// their state shown on the node itself. So Forge, Core and Equip cover
    /// every case, with Equip shared by Barracks, Camp, Arsenal and Sanctuary
    /// because those four differ only in which suits they permit.
    ///
    /// A plain class, not a MonoBehaviour, like LobbyPage: content is elements
    /// in a panel, not objects in a scene.
    ///
    /// THE BOUNDARY. Content reads SimulationState and never writes it. The only
    /// way any of these changes the game is by enqueuing a GameCommand on the
    /// InputBuffer, which is the one path the rules allow. Nothing here calls
    /// GameSimulation or CommandProcessor.
    /// </summary>
    public abstract class NodeSheetContent
    {
        public VisualElement Root { get; private set; }

        protected SimulationState State { get; private set; }
        protected InputBuffer Input { get; private set; }
        protected NodeWar.Core.ITickProvider Ticks { get; private set; }
        protected GameBalanceData Balance { get; private set; }

        /// <summary>The node this content is showing.</summary>
        protected int NodeID { get; private set; }

        /// <summary>The player whose side of the board we are looking from.</summary>
        protected int ControlledPID { get; private set; }

        /// <summary>True when the controlled player owns this node.</summary>
        protected bool IsOwned { get; private set; }

        protected NodeSheetContent()
        {
            Root = new VisualElement();
            Root.AddToClassList("sheet__content");
        }

        public void Bind(SimulationState state, InputBuffer input,
                         NodeWar.Core.ITickProvider ticks, GameBalanceData balance,
                         int nodeID, int controlledPID)
        {
            State = state;
            Input = input;
            Ticks = ticks;
            Balance = balance;
            NodeID = nodeID;
            ControlledPID = controlledPID;
            IsOwned = state.nodes[nodeID].ownerID == controlledPID;

            OnBind();
            Refresh();
        }

        /// <summary>Called once when the sheet opens on a node. Build here.</summary>
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

        /// <summary>
        /// Villagers standing at this node, owned by whoever owns the node.
        /// Consumed ones are skipped everywhere - a villager that breached is
        /// spent permanently and is not a unit any more.
        /// </summary>
        protected int CountWorkersHere()
        {
            int nodeOwner = State.nodes[NodeID].ownerID;
            int count = 0;

            for (int i = 0; i < State.villagers.Length; i++)
            {
                VillagerData v = State.villagers[i];

                if (v.currentNodeID != NodeID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (v.ownerID != nodeOwner) continue;

                count++;
            }

            return count;
        }

        protected static Label Caption(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("caption");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        protected static Label Body(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("body");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>
        /// The line a disabled control shows instead of doing nothing. Every
        /// refusal in this sheet names its reason, because the simulation
        /// silently drops a command it will not run and a button that does
        /// nothing is indistinguishable from a bug.
        /// </summary>
        protected static Label Reason(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("caption");
            label.AddToClassList("sheet__reason");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
    }
}
