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
        [SerializeField] private Color p0ClaimingColor = new Color(0.3f, 0.8f, 0.9f);
        [SerializeField] private Color p0FightingColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private Color p0IdleColor = new Color(0.3f, 0.6f, 1f);

        [Header("Player 1 (Red) Colors")]
        [SerializeField] private Color p1BaseColor = new Color(1f, 0.35f, 0.5f);
        [SerializeField] private Color p1MovingColor = new Color(1f, 0.6f, 0.4f);
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
        private VillagerState lastState = VillagerState.Idle;

        // Cached references
        private SpriteRenderer[] spriteRenderers;
        private MaterialPropertyBlock[] propBlocks;
        private Transform gfxTransform;

        private NodeWar.Core.TickRunner tickRunner;
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

        public void SetTickRunner(NodeWar.Core.TickRunner runner)
        {
            tickRunner = runner;
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

            if (villager.state == VillagerState.Dead || villager.isConsumed)
            {
                SetRenderersEnabled(false);
                return;
            }
            else
            {
                SetRenderersEnabled(true);
            }


            // === Position ===
            Vector3 targetPos;
            VillagerData villager_current = villager; // alias for clarity

            if (villager.state == VillagerState.Moving && villager.movePath.Length > 1 && tickRunner != null)
            {
                // Detect if we just started moving or advanced to a new edge
                bool justStartedMoving = (lastState != VillagerState.Moving);
                bool advancedEdge = (villager.movePathIndex != lastMovePathIndex && !justStartedMoving);

                if (justStartedMoving)
                {
                    // Started a new path — lerp from wherever we physically are right now
                    edgeStartWorldPos = transform.position;
                }
                else if (advancedEdge)
                {
                    // Crossed into a new node — start from that node's idle position
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
                        edgeStartWorldPos = simState.nodes[arrivedNodeID].worldPosition;
                    }
                }
                //adjust height
                edgeStartWorldPos.y = VillagerViewHeight;

                lastMovePathIndex = villager.movePathIndex;

                // Destination: idle position on the NEXT node in path
                Vector3 toPos;
                if (villager.movePathIndex + 1 < villager.movePath.Length)
                {
                    int nextNodeID = villager.movePath[villager.movePathIndex + 1];
                    if (nodeSlotManagers != null && nextNodeID < nodeSlotManagers.Length)
                    {
                        NodeSlotManager nextSlotManager = nodeSlotManagers[nextNodeID];
                        // Use a generic idle position (index 0 of 1) as the approach target
                        toPos = nextSlotManager.GetIdlePosition(0, 1);
                    }
                    else
                    {
                        toPos = simState.nodes[nextNodeID].worldPosition;
                    }
                }
                else
                {
                    toPos = edgeStartWorldPos; // shouldn't happen, safety
                }
                toPos.y = VillagerViewHeight;

                // Calculate lerp alpha
                int edgeWeight = GameSimulation.GetEdgeWeight(simState, villager.movePath[villager.movePathIndex], villager.movePath[villager.movePathIndex + 1]);
                int totalTicksForEdge = edgeWeight * villager.moveSpeedTicks;

                float edgeProgress = (float)villager.moveProgress / (float)totalTicksForEdge;
                float subTickAlpha = tickRunner.TickAlpha / (float)totalTicksForEdge;
                float totalAlpha = Mathf.Clamp01(edgeProgress + subTickAlpha);

                targetPos = Vector3.Lerp(edgeStartWorldPos, toPos, totalAlpha);
            }
            else if (nodeSlotManagers != null && villager.currentNodeID < nodeSlotManagers.Length)
            {
                // Not moving — use slot positions
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

                    default: // Idle
                        int idleIndex = GetLocalIndex(villager.currentNodeID, villager.ownerID, VillagerState.Idle);
                        int totalIdle = GetTotalOnNode(villager.currentNodeID, villager.ownerID, VillagerState.Idle);
                        targetPos = slotManager.GetIdlePosition(idleIndex, totalIdle);
                        break;
                }

                // Reset tracking when not moving
                lastMovePathIndex = -1;
            }
            else
            {
                targetPos = simState.nodes[villager.currentNodeID].worldPosition;
                lastMovePathIndex = -1;
            }

            // Track state for next frame's transition detection
            lastState = villager.state;
            targetPos.y = VillagerViewHeight;

            // Moving: use precise tick-based position. Not moving: smooth lerp to slot.
            if (villager.state == VillagerState.Moving)
            {
                transform.position = targetPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, slotLerpSpeed * Time.deltaTime);
            }
            // === Color ===
            Color stateColor = GetStateColor(villager);
            SetRenderersColor(stateColor);
            // Debug: update hierarchy name to reflect current state
#if UNITY_EDITOR
            gameObject.name = "V_" + villagerID + "_P" + villager.ownerID + "_" + villager.suit + "_" + villager.state;
            #endif
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
                    case VillagerState.Claiming: return p0ClaimingColor;
                    case VillagerState.Fighting: return p0FightingColor;
                    case VillagerState.Idle: return p0IdleColor;
                    case VillagerState.Working: return p0BaseColor;
                    default: return p0BaseColor;
                }
            }
            else
            {
                switch (villager.state)
                {
                    case VillagerState.Moving: return p1MovingColor;
                    case VillagerState.Claiming: return p1ClaimingColor;
                    case VillagerState.Fighting: return p1FightingColor;
                    case VillagerState.Idle: return p1IdleColor;
                    case VillagerState.Working: return p1BaseColor;
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