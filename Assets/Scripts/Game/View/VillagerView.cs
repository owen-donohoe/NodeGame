using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.View
{
    public class VillagerView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private float VillagerViewHeight = 0.1f;

        [Header("Movement")]
        [SerializeField] private float slotLerpSpeed = 8f;

        [Header("Player 0 (Blue) Colors")]
        [SerializeField] private Color p0BaseColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color p0MovingColor = new Color(0.5f, 0.75f, 1f);
        [SerializeField] private Color p0WorkingColor = new Color(0.2f, 0.8f, 0.4f);
        [SerializeField] private Color p0ClaimingColor = new Color(0.3f, 0.8f, 0.9f);
        [SerializeField] private Color p0FightingColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private Color p0IdleColor = new Color(0.3f, 0.6f, 1f);

        [Header("Player 1 (Red) Colors")]
        [SerializeField] private Color p1BaseColor = new Color(1f, 0.35f, 0.5f);
        [SerializeField] private Color p1MovingColor = new Color(1f, 0.6f, 0.4f);
        [SerializeField] private Color p1WorkingColor = new Color(0.9f, 0.6f, 0.2f);
        [SerializeField] private Color p1ClaimingColor = new Color(1f, 0.5f, 0.7f);
        [SerializeField] private Color p1FightingColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField] private Color p1IdleColor = new Color(1f, 0.35f, 0.5f);

        // Runtime
        private SimulationState simState;
        private int villagerID;
        private bool initialized = false;

        // Movement interpolation tracking
        private Vector3 edgeStartWorldPos;
        private int lastMovePathIndex = -1;
        private int lastLegFrom = -1;
        private int lastLegTo = -1;
        private VillagerState lastState = VillagerState.Idle;

        // Cached references
        private SpriteRenderer[] spriteRenderers;
        private MaterialPropertyBlock[] propBlocks;
        private Transform gfxTransform;

        private NodeWar.Core.ITickProvider tickProvider;
        private SelectionSystem selectionSystem;
        private NodeWar.View.NodeSlotManager[] nodeSlotManagers;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            villagerID = id;
            initialized = true;

            gfxTransform = transform.Find("GFX");

            if (gfxTransform != null)
            {
                spriteRenderers = gfxTransform.GetComponentsInChildren<SpriteRenderer>();
            }
            else
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            }

            propBlocks = new MaterialPropertyBlock[spriteRenderers.Length];
            for (int i = 0; i < propBlocks.Length; i++)
            {
                propBlocks[i] = new MaterialPropertyBlock();
            }

            UpdateVisuals();
        }

        public void SetTickProvider(NodeWar.Core.ITickProvider provider)
        {
            tickProvider = provider;
        }

        public void SetSelectionSystem(SelectionSystem system)
        {
            selectionSystem = system;
        }

        public void SetNodeSlotManagers(NodeWar.View.NodeSlotManager[] managers)
        {
            nodeSlotManagers = managers;
        }

        private void Update()
        {
            if (!initialized) return;

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            VillagerData villager = simState.villagers[villagerID];

            // === Visibility ===
            ApplyVisualState(villager);

            if (villager.state == VillagerState.Dead || villager.isConsumed)
                return;

            // === Position ===
            Vector3 targetPos;

            if (villager.state == VillagerState.Moving && villager.movePath.Length > 1 && tickProvider != null)
            {
                // The leg the simulation is actually walking. On a reversal
                // legFrom is the node the villager turned around before ever
                // reaching, and legTo is the node it is walking back to -- the
                // one case where legFrom is not currentNodeID.
                int legFrom = villager.movePath[villager.movePathIndex];
                int legTo = (villager.movePathIndex + 1 < villager.movePath.Length)
                    ? villager.movePath[villager.movePathIndex + 1]
                    : legFrom;

                bool justStartedMoving = (lastState != VillagerState.Moving);
                bool wasRerouted = (!justStartedMoving &&
                                    (legFrom != lastLegFrom || legTo != lastLegTo));
                bool advancedEdge = (!justStartedMoving && !wasRerouted &&
                                     villager.movePathIndex != lastMovePathIndex);

                if (justStartedMoving || wasRerouted || advancedEdge)
                {
                    // A leg always starts at movePath[movePathIndex], so that is
                    // the only honest anchor. A reroute used to anchor to the
                    // sprite instead, which drew the villager along a line the
                    // simulation was not walking: a diagonal across open board
                    // whenever the new route left in a different direction, and a
                    // crawl over the last stretch when it did not.
                    //
                    // Keying the reroute off the leg rather than off targetNodeID
                    // also catches re-issuing the same destination, which used to
                    // slip through and snap the sprite back to the path start.
                    edgeStartWorldPos = IdleSlotPosition(legFrom, villager.ownerID);
                }

                edgeStartWorldPos.y = VillagerViewHeight;
                lastMovePathIndex = villager.movePathIndex;
                lastLegFrom = legFrom;
                lastLegTo = legTo;

                Vector3 toPos;
                if (legTo != legFrom && nodeSlotManagers != null && legTo < nodeSlotManagers.Length &&
                    nodeSlotManagers[legTo] != null)
                {
                    toPos = nodeSlotManagers[legTo].GetIdlePosition(0, 1);
                }
                else
                {
                    toPos = edgeStartWorldPos;
                }
                toPos.y = VillagerViewHeight;

                int edgeWeight = GameSimulation.GetEdgeWeight(simState, legFrom, legTo);
                int totalTicksForEdge = edgeWeight * villager.moveSpeedTicks;
                if (totalTicksForEdge < 1) totalTicksForEdge = 1;

                float edgeProgress = (float)villager.moveProgress / (float)totalTicksForEdge;
                float subTickAlpha = tickProvider.TickAlpha / (float)totalTicksForEdge;
                float totalAlpha = Mathf.Clamp01(edgeProgress + subTickAlpha);

                targetPos = Vector3.Lerp(edgeStartWorldPos, toPos, totalAlpha);
            }
            else if (nodeSlotManagers != null && villager.currentNodeID < nodeSlotManagers.Length)
            {
                NodeSlotManager slotManager = nodeSlotManagers[villager.currentNodeID];

                switch (villager.state)
                {
                    case VillagerState.Working:
                        int workIndex = GetLocalIndex(villager.currentNodeID, villager.ownerID, VillagerState.Working);
                        targetPos = slotManager.GetWorkPosition(workIndex);
                        break;

                    case VillagerState.Claiming:
                        int claimIndex = GetLocalIndex(villager.currentNodeID, villager.ownerID, VillagerState.Claiming);
                        int totalClaiming = GetTotalOnNode(villager.currentNodeID, villager.ownerID, VillagerState.Claiming);
                        targetPos = slotManager.GetClaimPosition(claimIndex, totalClaiming);
                        break;

                    case VillagerState.Fighting:
                        int fightIndex = GetLocalIndexAllPlayers(villager.currentNodeID, VillagerState.Fighting);
                        int totalFighting = GetTotalOnNodeAllPlayers(villager.currentNodeID, VillagerState.Fighting);
                        targetPos = slotManager.GetFightPosition(fightIndex, totalFighting);
                        break;

                    default:
                        int idleIndex = GetLocalIndex(villager.currentNodeID, villager.ownerID, VillagerState.Idle);
                        int totalIdle = GetTotalOnNode(villager.currentNodeID, villager.ownerID, VillagerState.Idle);
                        targetPos = slotManager.GetIdlePosition(idleIndex, totalIdle);
                        break;
                }

                lastMovePathIndex = -1;
                lastLegFrom = -1;
                lastLegTo = -1;
            }
            else
            {
                targetPos = transform.position;
                lastMovePathIndex = -1;
                lastLegFrom = -1;
                lastLegTo = -1;
            }

            lastState = villager.state;
            targetPos.y = VillagerViewHeight;

            if (villager.state == VillagerState.Moving)
            {
                transform.position = targetPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, slotLerpSpeed * Time.deltaTime);
            }

#if UNITY_EDITOR
            gameObject.name = "V_" + villagerID + "_P" + villager.ownerID + "_" + villager.suit + "_" + villager.state;
#endif
        }

        /// <summary>
        /// Single entry point for all visual state: renderer enable/disable and color.
        /// Future additions (animator, sprite swap, costume overlays) go here.
        /// </summary>
        private void ApplyVisualState(VillagerData villager)
        {
            if (villager.state == VillagerState.Dead || villager.isConsumed)
            {
                SetRenderersEnabled(false);
                return;
            }

            SetRenderersEnabled(true);
            Color stateColor = GetStateColor(villager);

            // Flash composes over the state tint rather than replacing it, so a
            // fighting villager still reads as fighting mid-flash.
            if (flashAmount > 0f)
                stateColor = Color.Lerp(stateColor, Color.white, flashAmount);

            SetRenderersColor(stateColor);
        }

        private float flashAmount;

        /// <summary>
        /// 0 = no flash, 1 = fully white. Driven by VillagerFlash; kept here
        /// because this is the single entry point for villager visual state.
        /// </summary>
        public void SetFlashAmount(float amount)
        {
            flashAmount = Mathf.Clamp01(amount);
        }

        private void SetRenderersColor(Color color)
        {
            //ONLY THE FIRST ONE THIS IS FOR TESTING BECAUSE I JUST WANT ONE SPRITE TO BE COLORED CURRENTLY

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[0].color = color;
            }
        }

        private Color GetStateColor(VillagerData villager)
        {
            if (villager.ownerID == 0)
            {
                switch (villager.state)
                {
                    case VillagerState.Moving: return p0MovingColor;
                    case VillagerState.Working: return p0WorkingColor;
                    case VillagerState.Claiming: return p0ClaimingColor;
                    case VillagerState.Fighting: return p0FightingColor;
                    case VillagerState.Idle: return p0IdleColor;
                    default: return p0BaseColor;
                }
            }
            else
            {
                switch (villager.state)
                {
                    case VillagerState.Moving: return p1MovingColor;
                    case VillagerState.Working: return p1WorkingColor;
                    case VillagerState.Claiming: return p1ClaimingColor;
                    case VillagerState.Fighting: return p1FightingColor;
                    case VillagerState.Idle: return p1IdleColor;
                    default: return p1BaseColor;
                }
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].enabled = enabled;
            }
        }

        public int GetVillagerID()
        {
            return villagerID;
        }

        /// <summary>
        /// Where this villager stands on a node when idle, and so where movement
        /// interpolation should start from: a villager leaves a node from the
        /// same spot it was drawn standing on.
        ///
        /// Falls back to the current sprite position when the node has no slot
        /// manager, which keeps a missing reference from snapping the villager to
        /// the world origin.
        /// </summary>
        private Vector3 IdleSlotPosition(int nodeID, int ownerID)
        {
            if (nodeSlotManagers == null || nodeID < 0 || nodeID >= nodeSlotManagers.Length ||
                nodeSlotManagers[nodeID] == null)
            {
                return transform.position;
            }

            int idleIndex = GetLocalIndex(nodeID, ownerID, VillagerState.Idle);
            int totalIdle = GetTotalOnNode(nodeID, ownerID, VillagerState.Idle);
            return nodeSlotManagers[nodeID].GetIdlePosition(idleIndex, Mathf.Max(totalIdle, 1));
        }

        /// <summary>
        /// Gets this villager's index among same-owner villagers in the same state on the same node.
        /// </summary>
        private int GetLocalIndex(int nodeID, int ownerID, VillagerState targetState)
        {
            int index = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                if (i == villagerID) return index;
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != ownerID) continue;
                if (v.state != targetState) continue;
                if (v.isConsumed) continue;
                index++;
            }
            return 0;
        }

        /// <summary>
        /// Gets total same-owner villagers in a given state on a node.
        /// </summary>
        private int GetTotalOnNode(int nodeID, int ownerID, VillagerState targetState)
        {
            int count = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != ownerID) continue;
                if (v.state != targetState) continue;
                if (v.isConsumed) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Gets this villager's index among ALL players' villagers in a state on a node (for fighting).
        /// </summary>
        private int GetLocalIndexAllPlayers(int nodeID, VillagerState targetState)
        {
            int index = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                if (i == villagerID) return index;
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.state != targetState) continue;
                if (v.isConsumed) continue;
                index++;
            }
            return 0;
        }

        /// <summary>
        /// Gets total villagers (all players) in a state on a node.
        /// </summary>
        private int GetTotalOnNodeAllPlayers(int nodeID, VillagerState targetState)
        {
            int count = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.state != targetState) continue;
                if (v.isConsumed) continue;
                count++;
            }
            return count;
        }
    }
}