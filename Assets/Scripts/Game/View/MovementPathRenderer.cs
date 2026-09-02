using System.Collections.Generic;
using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Draws where the local player movers are headed, as a dotted route along
    /// the same curve the sprite walks.
    ///
    /// One renderer for the whole board rather than a component per villager,
    /// because routes are deduplicated. Villagers ordered together are almost
    /// always standing on the same node, so Pathfinding hands them the identical
    /// remaining node sequence -- drawing one line each would stack N copies of
    /// the same dashes on the same pixels. Keying the pool on distinct remaining
    /// routes instead means a squad reads as one line, and villagers coming from
    /// different nodes still get their own, merging where the routes genuinely
    /// coincide. Every line drawn is a route somebody is really walking; none is
    /// an average of several.
    ///
    /// Curve geometry comes from PathCurve, driven by the same PathCurveSettings
    /// instance VillagerView holds. That sharing is the point: a second copy of
    /// the corner radius that drifted would put every villager visibly beside its
    /// own route.
    ///
    /// Read-only over SimulationState, like every other view component.
    /// </summary>
    public class MovementPathRenderer : MonoBehaviour
    {
        private SimulationState simState;
        private int localPlayerID;
        private NodeSlotManager[] nodeSlotManagers;
        private NodeWar.Core.ITickProvider tickProvider;
        private PathCurveSettings settings = new PathCurveSettings();

        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private readonly List<Vector3> waypoints = new List<Vector3>();
        private readonly List<Vector3> remainder = new List<Vector3>();

        // Villagers whose route has already been drawn this frame. Compared
        // against, not hashed: a handful of movers makes the pairwise check
        // cheaper than building a key, and it allocates nothing.
        private readonly List<int> drawnThisFrame = new List<int>();

        // When each villager current route was ordered, for the fade. View-side
        // wall clock, deliberately: nothing here may touch simulation state.
        private float[] routeOrderedAt;
        private int[] lastTargetNode;

        private Material dashMaterial;

        public void Initialize(SimulationState state, int playerID,
                               PathCurveSettings curveSettings,
                               NodeWar.Core.ITickProvider provider)
        {
            simState = state;
            localPlayerID = playerID;
            tickProvider = provider;
            if (curveSettings != null) settings = curveSettings;
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
        }

        public void SetNodeSlotManagers(NodeSlotManager[] managers)
        {
            nodeSlotManagers = managers;
        }

        public void SetTickProvider(NodeWar.Core.ITickProvider provider)
        {
            tickProvider = provider;
        }

        /// <summary>
        /// LateUpdate so the routes are rebuilt after VillagerView has placed the
        /// sprites for this frame, and the head of a line sits on its villager
        /// rather than one frame behind it.
        /// </summary>
        private void LateUpdate()
        {
            if (simState == null || nodeSlotManagers == null) return;

            EnsureTracking();

            drawnThisFrame.Clear();
            int used = 0;

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData villager = simState.villagers[i];

                if (villager.ownerID != localPlayerID) continue;

                // Tracked before the movement filters, not after. An arrival
                // clears targetNodeID, and only by seeing that can a later order
                // back to the same node register as a new route rather than
                // inheriting the old one already-faded age.
                TrackOrderTime(i, villager);

                if (villager.isConsumed) continue;
                if (villager.state != VillagerState.Moving) continue;
                if (villager.movePath == null || villager.movePath.Length < 2) continue;
                if (villager.movePathIndex + 1 >= villager.movePath.Length) continue;

                if (AlreadyDrawn(i)) continue;
                if (!BuildRemainder(villager)) continue;
                if (remainder.Count < 2) continue;

                DrawRoute(used, i);
                drawnThisFrame.Add(i);
                used++;
            }

            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        /// <summary>
        /// True if some villager already drawn this frame is walking the same
        /// remaining node sequence. That is what collapses a squad to one line.
        /// </summary>
        private bool AlreadyDrawn(int villagerIndex)
        {
            for (int j = 0; j < drawnThisFrame.Count; j++)
            {
                if (SameRemainingRoute(villagerIndex, drawnThisFrame[j])) return true;
            }
            return false;
        }

        private bool SameRemainingRoute(int a, int b)
        {
            VillagerData va = simState.villagers[a];
            VillagerData vb = simState.villagers[b];

            int remainingA = va.movePath.Length - va.movePathIndex;
            int remainingB = vb.movePath.Length - vb.movePathIndex;
            if (remainingA != remainingB) return false;

            for (int k = 0; k < remainingA; k++)
            {
                if (va.movePath[va.movePathIndex + k] != vb.movePath[vb.movePathIndex + k])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Builds the whole route curve, then keeps only the stretch still to be
        /// walked. The whole route, because building from the current node would
        /// unround the corner the villager is banking through and shift the leg
        /// indices the sprite is placed by.
        /// </summary>
        private bool BuildRemainder(VillagerData villager)
        {
            waypoints.Clear();

            for (int i = 0; i < villager.movePath.Length; i++)
            {
                int nodeID = villager.movePath[i];
                if (nodeID < 0 || nodeID >= nodeSlotManagers.Length) return false;
                if (nodeSlotManagers[nodeID] == null) return false;

                Vector3 point = nodeSlotManagers[nodeID].transform.position;
                point.y = settings.lineHeight;
                waypoints.Add(point);
            }

            if (waypoints.Count < 2) return false;

            PathCurve.Build(waypoints, settings.cornerRadius, settings.cornerSegments);
            PathCurve.AppendRemainder(villager.movePathIndex, LegFraction(villager), remainder);
            return true;
        }

        /// <summary>
        /// How far through its current leg the villager is, matched to the same
        /// tick interpolation VillagerView uses so the line starts on the sprite.
        /// </summary>
        private float LegFraction(VillagerData villager)
        {
            int legFrom = villager.movePath[villager.movePathIndex];
            int legTo = villager.movePath[villager.movePathIndex + 1];

            int ticks = GameSimulation.GetEdgeWeight(simState, legFrom, legTo) * villager.moveSpeedTicks;
            if (ticks < 1) ticks = 1;

            float progress = (float)villager.moveProgress / (float)ticks;
            float subTick = tickProvider != null ? tickProvider.TickAlpha / (float)ticks : 0f;

            return Mathf.Clamp01(progress + subTick);
        }

        private void DrawRoute(int slot, int villagerIndex)
        {
            LineRenderer line = GetLine(slot);

            line.enabled = true;
            line.startWidth = settings.lineWidth;
            line.endWidth = settings.lineWidth;
            line.textureScale = new Vector2(settings.dashesPerUnit, 1f);

            // A route just ordered reads loudest and settles back as it runs, so
            // a board full of standing orders stays legible without any of them
            // disappearing. A dimmed route is still a checkable one.
            float age = Time.time - routeOrderedAt[villagerIndex];
            float settled = settings.settleSeconds > 0.0001f
                ? Mathf.Clamp01(age / settings.settleSeconds)
                : 1f;

            Color color = Color.Lerp(settings.freshColor, settings.settledColor, settled);
            line.startColor = color;
            line.endColor = color;

            line.positionCount = remainder.Count;
            for (int i = 0; i < remainder.Count; i++)
                line.SetPosition(i, remainder[i]);
        }

        private LineRenderer GetLine(int index)
        {
            while (pool.Count <= index)
            {
                GameObject go = new GameObject("Route_" + pool.Count);
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
                line.material = EnsureDashMaterial();
                line.enabled = false;

                pool.Add(line);
            }

            return pool[index];
        }

        /// <summary>
        /// The dashes come from a tiled 8x1 texture rather than an imported
        /// sprite, so this needs no asset and no serialized reference -- the same
        /// reason VillagerTouchTarget builds its collider at runtime.
        /// </summary>
        private Material EnsureDashMaterial()
        {
            if (dashMaterial != null) return dashMaterial;

            Texture2D texture = new Texture2D(8, 1, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;

            for (int x = 0; x < 8; x++)
            {
                bool ink = x < 4;
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, ink ? 1f : 0f));
            }
            texture.Apply();

            dashMaterial = new Material(Shader.Find("Sprites/Default"));
            dashMaterial.mainTexture = texture;

            return dashMaterial;
        }

        /// <summary>
        /// Villager arrays grow when a Village node is claimed, so the tracking
        /// arrays are sized lazily rather than once at startup.
        /// </summary>
        private void EnsureTracking()
        {
            int count = simState.villagers.Length;

            if (routeOrderedAt != null && routeOrderedAt.Length >= count) return;

            float[] grownTimes = new float[count];
            int[] grownTargets = new int[count];

            for (int i = 0; i < count; i++)
                grownTargets[i] = -1;

            if (routeOrderedAt != null)
            {
                for (int i = 0; i < routeOrderedAt.Length; i++)
                {
                    grownTimes[i] = routeOrderedAt[i];
                    grownTargets[i] = lastTargetNode[i];
                }
            }

            routeOrderedAt = grownTimes;
            lastTargetNode = grownTargets;
        }

        private void TrackOrderTime(int villagerIndex, VillagerData villager)
        {
            if (lastTargetNode[villagerIndex] == villager.targetNodeID) return;

            lastTargetNode[villagerIndex] = villager.targetNodeID;
            routeOrderedAt[villagerIndex] = Time.time;
        }

        private void OnDestroy()
        {
            if (dashMaterial != null)
            {
                if (dashMaterial.mainTexture != null) Destroy(dashMaterial.mainTexture);
                Destroy(dashMaterial);
            }
        }
    }
}
