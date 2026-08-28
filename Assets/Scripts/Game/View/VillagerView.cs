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
        private int lastTargetNodeID = -1; 
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
                bool justStartedMoving = (lastState != VillagerState.Moving);
                bool wasRerouted = (!justStartedMoving && villager.targetNodeID != lastTargetNodeID && lastTargetNodeID != -1);
                bool advancedEdge = (!justStartedMoving && !wasRerouted &&
                                     villager.movePathIndex != lastMovePathIndex);

                if (justStartedMoving || wasRerouted)
                {
                    edgeStartWorldPos = transform.position;
                }
                else if (advancedEdge)
                {
                    int arrivedNodeID = villager.movePath[villager.movePathIndex];
                    if (nodeSlotManagers != null && arrivedNodeID < nodeSlotManagers.Length)
                    {
                        NodeSlotManager arrivalSlotManager = nodeSlotManagers[arrivedNodeID];
                        int idleIdx = GetLocalIndex(arrivedNodeID, villager.ownerID, VillagerState.Idle);
                        int totalIdleOnArrival = GetTotalOnNode(arrivedNodeID, villager.ownerID, VillagerState.Idle);
                        edgeStartWorldPos = arrivalSlotManager.GetIdlePosition(idleIdx, Mathf.Max(totalIdleOnArrival, 1));
                    }
                    else
                    {
                        edgeStartWorldPos = transform.position;
                    }
                }

                edgeStartWorldPos.y = VillagerViewHeight;
                lastMovePathIndex = villager.movePathIndex;
                lastTargetNodeID = villager.targetNodeID;

                Vector3 toPos;
                if (villager.movePathIndex + 1 < villager.movePath.Length)
                {
                    int nextNodeID = villager.movePath[villager.movePathIndex + 1];
                    if (nodeSlotManagers != null && nextNodeID < nodeSlotManagers.Length)
                    {
                        NodeSlotManager nextSlotManager = nodeSlotManagers[nextNodeID];
                        toPos = nextSlotManager.GetIdlePosition(0, 1);
                    }
                    else
                    {
                        toPos = edgeStartWorldPos;
                    }
                }
                else
                {
                    toPos = edgeStartWorldPos;
                }
                toPos.y = VillagerViewHeight;

                int edgeWeight = GameSimulation.GetEdgeWeight(simState,
                    villager.movePath[villager.movePathIndex],
                    villager.movePath[villager.movePathIndex + 1]);
                int totalTicksForEdge = edgeWeight * villager.moveSpeedTicks;

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
                lastTargetNodeID = -1;
            }
            else
            {
                targetPos = transform.position;
                lastMovePathIndex = -1;
                lastTargetNodeID = -1;
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
            SetRenderersColor(stateColor);
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