using System.Collections.Generic;
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
        private VillagerState lastState = VillagerState.Idle;

        // Offset carried out of a crowded node so departure does not snap the
        // sprite from its idle slot to the node centre the route runs through.
        private Vector3 departureOffset;
        private readonly List<Vector3> routeWaypoints = new List<Vector3>();

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

        /// <summary>
        /// Curve shape shared with MovementPathRenderer, so the route the sprite
        /// walks and the route drawn on the board are the same curve. Falls back
        /// to defaults if nothing supplies one, the way SelectionSystem falls
        /// back for gesture thresholds.
        /// </summary>
        public void SetPathCurveSettings(PathCurveSettings settings)
        {
            if (settings != null) curveSettings = settings;
        }

        private PathCurveSettings curveSettings = new PathCurveSettings();

        /// <summary>
        /// Builds the curve for the whole route into PathCurve.
        ///
        /// Waypoints are node centres, not per-villager idle slots: two villagers
        /// ordered together must produce the same curve, or MovementPathRenderer
        /// cannot collapse their routes to one line, and each would ride a curve
        /// of its own. The slot offset is carried separately as departureOffset.
        ///
        /// Returns false if any node on the route has no slot manager, rather
        /// than building a curve with a hole in it -- a missing waypoint would
        /// shift every leg index after it and put the sprite on the wrong leg.
        /// </summary>
        private bool BuildRouteCurve(VillagerData villager)
        {
            if (nodeSlotManagers == null) return false;

            routeWaypoints.Clear();

            for (int i = 0; i < villager.movePath.Length; i++)
            {
                int nodeID = villager.movePath[i];
                if (nodeID < 0 || nodeID >= nodeSlotManagers.Length) return false;
                if (nodeSlotManagers[nodeID] == null) return false;

                Vector3 point = nodeSlotManagers[nodeID].transform.position;
                point.y = VillagerViewHeight;
                routeWaypoints.Add(point);
            }

            if (routeWaypoints.Count < 2) return false;

            PathCurve.Build(routeWaypoints, curveSettings.cornerRadius, curveSettings.cornerSegments);
            return true;
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
                int legIndex = villager.movePathIndex;
                int legFrom = villager.movePath[legIndex];
                int legTo = (legIndex + 1 < villager.movePath.Length)
                    ? villager.movePath[legIndex + 1]
                    : legFrom;

                int edgeWeight = GameSimulation.GetEdgeWeight(simState, legFrom, legTo);
                int totalTicksForEdge = edgeWeight * villager.moveSpeedTicks;
                if (totalTicksForEdge < 1) totalTicksForEdge = 1;

                float edgeProgress = (float)villager.moveProgress / (float)totalTicksForEdge;
                float subTickAlpha = tickProvider.TickAlpha / (float)totalTicksForEdge;
                float legT = Mathf.Clamp01(edgeProgress + subTickAlpha);

                // The whole route, not just the stretch ahead. Building from the
                // current node would unround the corner the villager is banking
                // through at the moment it crosses a leg boundary, and the sprite
                // would jump from the corner to the node centre once per node.
                if (BuildRouteCurve(villager))
                {
                    targetPos = PathCurve.PositionOnLeg(legIndex, legT);
                }
                else
                {
                    targetPos = transform.position;
                }
                targetPos.y = VillagerViewHeight;

                // A villager standing in a crowded node sits in its idle slot,
                // while the route runs through the node centre. Carry that offset
                // out of the node and let it decay, so a move order does not snap
                // the villager to the middle of the node it is leaving.
                if (lastState != VillagerState.Moving)
                    departureOffset = transform.position - targetPos;
                else
                    departureOffset = Vector3.Lerp(departureOffset, Vector3.zero,
                                                   slotLerpSpeed * Time.deltaTime);

                departureOffset.y = 0f;
                targetPos += departureOffset;
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
            }
            else
            {
                targetPos = transform.position;
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