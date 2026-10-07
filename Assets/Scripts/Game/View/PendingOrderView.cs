using System.Collections.Generic;
using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Acknowledges a move order the frame it is issued, ahead of lockstep.
    ///
    /// The order reaches the simulation two or more ticks later, and until then
    /// the villager looks as if nothing was said to it. This draws a provisional
    /// route -- the player's colour at reduced alpha, in the dash style of the
    /// real routes -- and pulses each ordered villager. When the simulation
    /// shows the destination, MovementPathRenderer's line takes over and this one
    /// is dropped in the same LateUpdate, so the handover neither flickers nor
    /// draws twice. PendingOrderTracker owns when that happens.
    ///
    /// The route is FindPath over the same state the simulation will use, so it
    /// is a close approximation rather than a promise: it ignores the rounded
    /// corners, and a villager mid-edge that is ordered back the way it came
    /// is drawn heading back to the node it left first, as the simulation does.
    ///
    /// Routes are deduplicated like MovementPathRenderer's: a squad standing on
    /// one node and sent to one place reads as one line.
    ///
    /// Read-only over SimulationState; never writes a command.
    /// </summary>
    public class PendingOrderView : MonoBehaviour
    {
        private const float TimeoutSeconds = 1.5f;

        // Fraction of the real routes' freshly-ordered alpha, so the line reads
        // as provisional beside the one that replaces it.
        private const float ProvisionalAlphaScale = 0.5f;

        // Two mid-edge villagers only share a line if they are this close.
        private const float SharedStartDistance = 0.3f;

        private SimulationState simState;
        private int localPlayerID;
        private VillagerPositioner[] nodeSlotManagers;
        private Transform[] villagerTransforms;
        private PathCurveSettings settings = new PathCurveSettings();
        private MovementPathRenderer pathRenderer;

        private readonly PendingOrderTracker tracker = new PendingOrderTracker(TimeoutSeconds);
        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private readonly List<PendingOrderTracker.Order> drawn = new List<PendingOrderTracker.Order>();

        // Order drag preview: the same route and look as a pending order, drawn
        // while the finger is still down. Release hands over to OnMoveIssued, which
        // builds the identical route for the same frame.
        private readonly List<PendingOrderTracker.Order> hover = new List<PendingOrderTracker.Order>();
        private readonly List<int> hoverVillagers = new List<int>();
        private int hoverNode = -1;
        private readonly Gradient gradient = new Gradient();
        private readonly GradientColorKey[] colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        private SortingLayer[] sortingLayers;

        public void Initialize(SimulationState state, int playerID,
                               PathCurveSettings curveSettings,
                               MovementPathRenderer routes)
        {
            simState = state;
            localPlayerID = playerID;
            pathRenderer = routes;

            if (curveSettings != null) settings = curveSettings;
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
            tracker.Clear();
        }

        public void SetNodeSlotManagers(VillagerPositioner[] managers)
        {
            nodeSlotManagers = managers;
        }

        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
        }

        /// <summary>
        /// Subscribed to CommandSystem.MoveIssued by GameManager.
        /// </summary>
        public void OnMoveIssued(int targetNode, IReadOnlyList<int> villagerIDs)
        {
            if (simState == null || nodeSlotManagers == null) return;

            float now = Time.unscaledTime;

            for (int i = 0; i < villagerIDs.Count; i++)
            {
                int id = villagerIDs[i];
                if (id < 0 || id >= simState.villagers.Length) continue;

                VillagerData villager = simState.villagers[id];
                if (villager.ownerID != localPlayerID) continue;

                Pulse(id);

                // Already walking there: the real route is on screen and there
                // is nothing provisional to add. Cancel rather than skip, so an
                // older pending order for another place does not outlive this.
                if (villager.state == VillagerState.Moving && villager.targetNodeID == targetNode)
                {
                    tracker.Cancel(id);
                    continue;
                }

                int[] route = BuildRoute(villager, targetNode);
                if (route == null)
                {
                    tracker.Cancel(id);
                    continue;
                }

                tracker.Issue(id, targetNode, route, now);
            }
        }

        /// <summary>Subscribed to CommandSystem.OrderDragBegan.</summary>
        public void BeginHover(IReadOnlyList<int> villagerIDs)
        {
            hoverVillagers.Clear();
            for (int i = 0; i < villagerIDs.Count; i++) hoverVillagers.Add(villagerIDs[i]);
            SetHoverNode(-1);
        }

        /// <summary>Subscribed to CommandSystem.OrderDragHover; -1 when no node is under the finger.</summary>
        public void SetHoverNode(int node)
        {
            hover.Clear();
            hoverNode = node;
            if (node < 0 || simState == null || nodeSlotManagers == null) return;

            for (int i = 0; i < hoverVillagers.Count; i++)
            {
                int id = hoverVillagers[i];
                if (id < 0 || id >= simState.villagers.Length) continue;

                VillagerData villager = simState.villagers[id];
                if (villager.ownerID != localPlayerID) continue;
                if (villager.state == VillagerState.Moving && villager.targetNodeID == node) continue;

                int[] route = BuildRoute(villager, node);
                if (route == null) continue;

                hover.Add(new PendingOrderTracker.Order { villagerID = id, targetNode = node, routeNodes = route });
            }
        }

        /// <summary>Subscribed to CommandSystem.OrderDragEnded.</summary>
        public void EndHover()
        {
            hoverVillagers.Clear();
            hover.Clear();
            hoverNode = -1;
        }

        /// <summary>
        /// The nodes the provisional line passes through after the villager's own
        /// position, or null if the simulation would refuse the order.
        /// </summary>
        private int[] BuildRoute(VillagerData villager, int targetNode)
        {
            int from = villager.currentNodeID;
            if (from == targetNode) return null;

            int[] path = Pathfinding.FindPath(simState, villager.ownerID, from, targetNode);
            if (path == null || path.Length < 2) return null;

            // Mid-edge, the crossing already covered is kept if the route runs on
            // through the node being approached; otherwise the villager walks
            // back to the node it left first. Standing, the first node is where
            // it already is.
            int start = IsMidEdge(villager) && path[1] != OtherEnd(villager) ? 0 : 1;

            int[] nodes = new int[path.Length - start];
            for (int i = 0; i < nodes.Length; i++)
                nodes[i] = path[start + i];

            return nodes;
        }

        private static bool IsMidEdge(VillagerData villager)
        {
            if (villager.state != VillagerState.Moving) return false;
            if (villager.movePath == null) return false;
            if (villager.movePathIndex + 1 >= villager.movePath.Length) return false;

            // A reversal leg (movePath[index] != currentNodeID) only exists
            // once something has been crossed; a forward leg has to have started.
            return villager.movePath[villager.movePathIndex] != villager.currentNodeID ||
                   villager.moveProgress > 0;
        }

        private static int OtherEnd(VillagerData villager)
        {
            int legFrom = villager.movePath[villager.movePathIndex];
            int legTo = villager.movePath[villager.movePathIndex + 1];
            return legFrom == villager.currentNodeID ? legTo : legFrom;
        }

        private void Pulse(int villagerID)
        {
            if (villagerTransforms == null || villagerID >= villagerTransforms.Length) return;
            if (villagerTransforms[villagerID] == null) return;

            VillagerFlash flash = villagerTransforms[villagerID].GetComponent<VillagerFlash>();
            if (flash != null) flash.Flash();
        }

        /// <summary>
        /// LateUpdate, like MovementPathRenderer, so the head of a line sits on
        /// its villager for this frame. The sweep comes first so a route the
        /// simulation has taken over is gone before anything is drawn.
        /// </summary>
        private void LateUpdate()
        {
            if (simState == null || nodeSlotManagers == null) return;

            tracker.Sweep(Time.unscaledTime, simState.villagers);

            drawn.Clear();
            int used = 0;
            IReadOnlyList<PendingOrderTracker.Order> orders = tracker.Orders;

            for (int i = 0; i < orders.Count; i++)
                used = TryDraw(orders[i], used);
            for (int i = 0; i < hover.Count; i++)
            {
                VillagerData villager = simState.villagers[hover[i].villagerID];
                if (villager.isConsumed || villager.state == VillagerState.Dead) continue;
                used = TryDraw(hover[i], used);
            }

            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        private int TryDraw(PendingOrderTracker.Order order, int used)
        {
            if (AlreadyDrawn(order)) return used;
            if (!DrawOrder(used, order)) return used;

            drawn.Add(order);
            return used + 1;
        }

        private bool AlreadyDrawn(PendingOrderTracker.Order order)
        {
            bool midEdge = IsMidEdge(simState.villagers[order.villagerID]);

            for (int j = 0; j < drawn.Count; j++)
            {
                PendingOrderTracker.Order other = drawn[j];
                if (!SameNodes(order.routeNodes, other.routeNodes)) continue;

                bool otherMidEdge = IsMidEdge(simState.villagers[other.villagerID]);
                if (!midEdge && !otherMidEdge) return true;
                if (midEdge != otherMidEdge) continue;

                Vector3 a = VillagerPosition(order.villagerID);
                Vector3 b = VillagerPosition(other.villagerID);
                if ((a - b).sqrMagnitude <= SharedStartDistance * SharedStartDistance) return true;
            }

            return false;
        }

        private static bool SameNodes(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private Vector3 VillagerPosition(int villagerID)
        {
            if (villagerTransforms != null && villagerID < villagerTransforms.Length &&
                villagerTransforms[villagerID] != null)
                return villagerTransforms[villagerID].position;

            // No sprite to start from: begin at the node it stands on.
            int node = simState.villagers[villagerID].currentNodeID;
            return node >= 0 && node < nodeSlotManagers.Length && nodeSlotManagers[node] != null
                ? nodeSlotManagers[node].transform.position
                : Vector3.zero;
        }

        private bool DrawOrder(int slot, PendingOrderTracker.Order order)
        {
            int[] nodes = order.routeNodes;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] < 0 || nodes[i] >= nodeSlotManagers.Length) return false;
                if (nodeSlotManagers[nodes[i]] == null) return false;
            }

            LineRenderer line = GetLine(slot);
            line.enabled = true;
            line.startWidth = settings.lineWidth;
            line.endWidth = settings.lineWidth;
            line.textureScale = new Vector2(1f / Mathf.Max(0.01f, settings.dashSpacing), 1f);

            Color color = localPlayerID == 0 ? settings.player0Color : settings.player1Color;
            float alpha = Mathf.Clamp01(settings.freshAlpha * ProvisionalAlphaScale);

            colorKeys[0].color = color;
            colorKeys[0].time = 0f;
            colorKeys[1].color = color;
            colorKeys[1].time = 1f;
            alphaKeys[0].alpha = alpha;
            alphaKeys[0].time = 0f;
            alphaKeys[1].alpha = alpha;
            alphaKeys[1].time = 1f;
            gradient.SetKeys(colorKeys, alphaKeys);
            line.colorGradient = gradient;

            line.positionCount = nodes.Length + 1;
            line.SetPosition(0, AtLineHeight(VillagerPosition(order.villagerID)));
            for (int i = 0; i < nodes.Length; i++)
                line.SetPosition(i + 1, AtLineHeight(nodeSlotManagers[nodes[i]].transform.position));

            return true;
        }

        private Vector3 AtLineHeight(Vector3 p)
        {
            p.y = settings.lineHeight;
            return p;
        }

        private LineRenderer GetLine(int index)
        {
            while (pool.Count <= index)
            {
                GameObject go = new GameObject("PendingRoute_" + pool.Count);
                go.transform.SetParent(transform, false);

                LineRenderer line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.numCornerVertices = 2;
                line.numCapVertices = 0;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Tile;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = pathRenderer != null ? pathRenderer.DashMaterial : null;

                // Same layer as the real routes, resolved the same way.
                int layerID = SortingLayer.NameToID(settings.sortingLayerName);
                if (!SortingLayer.IsValid(layerID))
                {
                    if (sortingLayers == null) sortingLayers = SortingLayer.layers;
                    if (sortingLayers.Length > 0) layerID = sortingLayers[0].id;
                }
                line.sortingLayerID = layerID;
                line.sortingOrder = short.MinValue;
                line.enabled = false;

                pool.Add(line);
            }

            return pool[index];
        }
    }
}
