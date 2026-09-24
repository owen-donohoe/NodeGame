using System.Collections.Generic;
using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Draws movement intent: your own routes in full, an opponent route cut
    /// short and dissolving.
    ///
    /// One renderer for the whole board rather than a component per villager,
    /// because routes are deduplicated. Villagers ordered together are almost
    /// always standing on the same node, so Pathfinding hands them the identical
    /// remaining node sequence -- drawing one line each would stack N copies of
    /// the same dashes on the same pixels. Keying the pool on distinct routes
    /// instead means a squad reads as one line, and villagers coming from
    /// different nodes still get their own, merging where the routes genuinely
    /// coincide. Every line drawn is a route somebody is really walking.
    ///
    /// Curve geometry is read from RouteCurveCache, which VillagerView populates
    /// for its own villager every frame it moves. VillagerView is the one place
    /// the curve is built; a second copy of the corner radius that drifted would
    /// put every villager visibly beside its own route, so this reads the same
    /// curve rather than rebuilding one from PathCurveSettings itself.
    ///
    /// The opponent half is an information gate, and the gate is the geometry.
    /// Drawing a whole route and tapering its alpha would leave the destination
    /// on screen for anyone who raises their brightness; the route is truncated
    /// instead, and the fade only shapes what survives the cut. Two conditions
    /// bound it further, and they cover each other: on-screen alone would reward
    /// zooming out, and a hop range alone would let a route pull attention off
    /// the visible board.
    ///
    /// Read-only over SimulationState, like every other view component.
    /// </summary>
    public class MovementPathRenderer : MonoBehaviour
    {
        private SimulationState simState;
        private int localPlayerID;
        private NodeSlotManager[] nodeSlotManagers;
        private NodeWar.Core.ITickProvider tickProvider;
        private Camera cam;

        private PathCurveSettings settings = new PathCurveSettings();
        private OpponentRouteSettings opponentSettings = new OpponentRouteSettings();

        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private readonly List<Vector3> remainder = new List<Vector3>();

        // Villagers whose route has already been drawn this frame, kept apart by
        // side: an opponent route is compared only over the stretch that is
        // actually shown, so two opponents sharing a visible stub collapse to one
        // line even when they diverge past the cut.
        private readonly List<int> drawnOwn = new List<int>();
        private readonly List<int> drawnOpponent = new List<int>();

        // When each villager current route was ordered, for the fade on your own
        // side. View-side wall clock, deliberately: nothing here touches the
        // simulation.
        private float[] routeOrderedAt;
        private int[] lastTargetNode;

        private readonly OpponentRouteGate opponentGate = new OpponentRouteGate();

        // Geometry of the stub drawn this pass, so the node-based fade can be
        // converted into a fraction of it.
        private float lastStubLength;
        private float lastNodeSpacing;

        private Material dashMaterial;
        private SortingLayer[] sortingLayers;

        private readonly Gradient gradient = new Gradient();
        private readonly GradientColorKey[] colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];

        public void Initialize(SimulationState state, int playerID,
                               PathCurveSettings curveSettings,
                               OpponentRouteSettings opponentRouteSettings,
                               NodeWar.Core.ITickProvider provider,
                               Camera camera)
        {
            simState = state;
            localPlayerID = playerID;
            tickProvider = provider;
            cam = camera != null ? camera : Camera.main;

            if (curveSettings != null) settings = curveSettings;
            if (opponentRouteSettings != null) opponentSettings = opponentRouteSettings;
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

            // The BFS only feeds OpponentRouteVisible's hop-range gate, which
            // itself returns false immediately when opponent routes are off.
            // Skip the board-wide walk entirely rather than paying for it and
            // then discarding the result villager by villager.
            if (opponentSettings.show)
                opponentGate.ComputeHopsFromPlayer(simState, localPlayerID, opponentSettings.withinHopsOfYou);

            drawnOwn.Clear();
            drawnOpponent.Clear();
            int used = 0;

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData villager = simState.villagers[i];
                bool mine = villager.ownerID == localPlayerID;

                // Tracked before the movement filters, not after. An arrival
                // clears targetNodeID, and only by seeing that can a later order
                // back to the same node register as a new route rather than
                // inheriting the old one already-faded age.
                if (mine) TrackOrderTime(i, villager);

                if (villager.isConsumed) continue;
                if (villager.state != VillagerState.Moving) continue;
                if (villager.movePath == null || villager.movePath.Length < 2) continue;
                if (villager.movePathIndex + 1 >= villager.movePath.Length) continue;

                float screenAlpha = 1f;
                if (!mine && !OpponentRouteVisible(villager, out screenAlpha)) continue;

                int legs = mine ? int.MaxValue :
                    RouteReveal.LegCount(villager.movePath, villager.movePathIndex, opponentSettings.revealLegs);
                int compareNodes = mine ? int.MaxValue : opponentSettings.revealLegs + 1;

                if (AlreadyDrawn(i, mine ? drawnOwn : drawnOpponent, compareNodes)) continue;
                if (!BuildRemainder(villager, i, mine, legs)) continue;
                if (remainder.Count < 2) continue;

                DrawRoute(used, i, mine, screenAlpha);
                (mine ? drawnOwn : drawnOpponent).Add(i);
                used++;
            }

            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        /// <summary>
        /// Both gates an opponent route has to pass, and how strongly it draws if
        /// it does. Neither gate is sufficient alone: the screen falloff by itself
        /// would let a player zoom out to see the whole board, and the hop range
        /// by itself would draw routes for villagers the player cannot see.
        ///
        /// screenAlpha comes back as a multiplier rather than a yes or no, so a
        /// villager leaving the view dims out over offScreenFade instead of its
        /// route blinking off the instant it crosses the edge.
        /// </summary>
        private bool OpponentRouteVisible(VillagerData villager, out float screenAlpha)
        {
            screenAlpha = 1f;

            if (!opponentSettings.show) return false;

            int node = villager.currentNodeID;

            if (!opponentGate.WithinHops(node, opponentSettings.withinHopsOfYou)) return false;

            if (cam == null) return false;
            if (node < 0 || node >= nodeSlotManagers.Length) return false;
            if (nodeSlotManagers[node] == null) return false;

            Vector3 viewport = cam.WorldToViewportPoint(nodeSlotManagers[node].transform.position);
            if (viewport.z <= 0f) return false;   // behind the camera

            // How far outside the unit viewport box, in screen widths.
            float outX = Mathf.Max(0f, Mathf.Max(-viewport.x, viewport.x - 1f));
            float outY = Mathf.Max(0f, Mathf.Max(-viewport.y, viewport.y - 1f));
            float outside = Mathf.Max(outX, outY);

            if (outside <= 0f) return true;   // fully on screen

            float range = opponentSettings.offScreenFade;
            if (range <= 0.0001f) return false;   // hard cut at the edge
            if (outside >= range) return false;

            screenAlpha = 1f - (outside / range);
            return screenAlpha > 0.001f;
        }

        /// <summary>
        /// True if a villager already drawn this frame walks the same route over
        /// the stretch that will actually be shown. That is what collapses a
        /// squad to one line.
        /// </summary>
        private bool AlreadyDrawn(int villagerIndex, List<int> drawn, int compareNodes)
        {
            for (int j = 0; j < drawn.Count; j++)
            {
                if (SameRoute(villagerIndex, drawn[j], compareNodes)) return true;
            }
            return false;
        }

        private bool SameRoute(int a, int b, int compareNodes)
        {
            VillagerData va = simState.villagers[a];
            VillagerData vb = simState.villagers[b];

            return RouteReveal.SameRoute(va.movePath, va.movePathIndex,
                                         vb.movePath, vb.movePathIndex, compareNodes);
        }

        /// <summary>
        /// Reads the whole route curve from RouteCurveCache -- built this same
        /// frame by this villager's VillagerView, whose Update runs before this
        /// LateUpdate -- then keeps the stretch to be drawn. The whole route,
        /// because building from the current node would unround the corner the
        /// villager is banking through and shift the leg indices the sprite is
        /// placed by.
        ///
        /// False if nothing is cached yet, which only happens for a villager
        /// whose VillagerView could not place it this frame either (e.g. no tick
        /// provider) -- the route line and the sprite it belongs to fail the
        /// same way rather than one drawing without the other.
        /// </summary>
        private bool BuildRemainder(VillagerData villager, int villagerIndex, bool mine, int legs)
        {
            if (!RouteCurveCache.TryGetCurrent(villagerIndex, villager.movePath, out List<Vector3> curvePoints, out List<int> curveLegStarts))
                return false;

            int fromNode = villager.movePath[villager.movePathIndex];
            int toNode = villager.movePath[villager.movePathIndex + 1];

            if (fromNode < 0 || fromNode >= nodeSlotManagers.Length) return false;
            if (toNode < 0 || toNode >= nodeSlotManagers.Length) return false;
            if (nodeSlotManagers[fromNode] == null || nodeSlotManagers[toNode] == null) return false;

            // Measured once here so DrawRoute can express the fade in nodes
            // rather than in the 0..1 line parameter, which would stretch and
            // shrink with wherever the truncation happened to land. Straight-line
            // node spacing, not the rounded curve length -- this is only a
            // yardstick for that fade and has always meant the Euclidean
            // distance between the two centres.
            lastNodeSpacing = (nodeSlotManagers[toNode].transform.position -
                               nodeSlotManagers[fromNode].transform.position).magnitude;

            PathCurve.AppendRemainder(curvePoints, curveLegStarts, villager.movePathIndex, LegFraction(villager), legs, remainder);
            if (remainder.Count == 0) return false;

            // The cached curve is built at VillagerViewHeight, the sprite's
            // height -- not settings.lineHeight, the line's own. Restamp the
            // height the route has always drawn at rather than letting a route
            // sourced from someone else's cache silently take on their height.
            float height = settings.lineHeight;
            for (int i = 0; i < remainder.Count; i++)
            {
                Vector3 p = remainder[i];
                p.y = height;
                remainder[i] = p;
            }

            lastStubLength = 0f;
            for (int i = 1; i < remainder.Count; i++)
                lastStubLength += (remainder[i] - remainder[i - 1]).magnitude;

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

        private void DrawRoute(int slot, int villagerIndex, bool mine, float screenAlpha)
        {
            LineRenderer line = GetLine(slot);

            float width = mine ? settings.lineWidth : settings.lineWidth * opponentSettings.widthScale;

            line.enabled = true;
            line.startWidth = width;
            line.endWidth = width;

            // textureScale is repeats per unit; the setting is the distance
            // between dashes, which is its reciprocal and the thing that is
            // actually legible when tuning.
            line.textureScale = new Vector2(1f / Mathf.Max(0.01f, settings.dashSpacing), 1f);

            // A route carries the colour of whoever owns the villager, not of
            // which side is looking, so the opponent route is always the
            // opponent colour whichever player the local one happens to be.
            Color color = PlayerColor(simState.villagers[villagerIndex].ownerID);

            if (mine)
            {
                // A route just ordered reads loudest and settles back as it runs,
                // so a board full of standing orders stays legible without any of
                // them disappearing. A dimmed route is still a checkable one.
                float age = Time.time - routeOrderedAt[villagerIndex];
                float settled = settings.settleSeconds > 0.0001f
                    ? Mathf.Clamp01(age / settings.settleSeconds)
                    : 1f;

                float alpha = Mathf.Lerp(settings.freshAlpha, settings.settledAlpha, settled);
                // Flat: no distance fade on your own routes, and no off-screen
                // dimming either -- your own intent is yours to see.
                ApplyGradient(line, color, alpha, alpha, 1f, 1f);
            }
            else
            {
                // Certain at the villager, dissolving as it goes, so the line
                // reads as knowledge running out rather than as a route that
                // simply stops.
                //
                // The fade is measured in nodes and converted to a fraction of
                // this stub, so it means the same distance whatever length the
                // stub happens to be -- a fade defined straight on the 0..1 line
                // parameter would stretch and shrink with the truncation.
                float fadeDistance = opponentSettings.fadeNodes * lastNodeSpacing;
                float fadeAt = lastStubLength > 0.0001f
                    ? Mathf.Clamp01(fadeDistance / lastStubLength)
                    : 1f;

                ApplyGradient(line, color, opponentSettings.nearAlpha,
                              opponentSettings.farAlpha, fadeAt, screenAlpha);
            }

            line.positionCount = remainder.Count;
            for (int i = 0; i < remainder.Count; i++)
                line.SetPosition(i, remainder[i]);
        }

        private Color PlayerColor(int ownerID)
        {
            return ownerID == 0 ? settings.player0Color : settings.player1Color;
        }

        /// <summary>
        /// Colour along the line: one hue, opacity running from nearAlpha at the
        /// villager to farAlpha at fadeAt, and flat beyond that because a
        /// Gradient holds its last key.
        ///
        /// multiplier scales the whole thing, which is how the off-screen falloff
        /// dims a route without touching the shape of its fade.
        ///
        /// Reused key arrays rather than fresh ones, since this runs per route
        /// per frame.
        /// </summary>
        private void ApplyGradient(LineRenderer line, Color color,
                                   float nearAlpha, float farAlpha,
                                   float fadeAt, float multiplier)
        {
            if (fadeAt < 0.001f) fadeAt = 0.001f;

            colorKeys[0].color = color;
            colorKeys[0].time = 0f;
            colorKeys[1].color = color;
            colorKeys[1].time = 1f;

            alphaKeys[0].alpha = Mathf.Clamp01(nearAlpha * multiplier);
            alphaKeys[0].time = 0f;
            alphaKeys[1].alpha = Mathf.Clamp01(Mathf.Lerp(nearAlpha, farAlpha, 0.5f) * multiplier);
            alphaKeys[1].time = fadeAt * 0.5f;
            alphaKeys[2].alpha = Mathf.Clamp01(farAlpha * multiplier);
            alphaKeys[2].time = fadeAt;

            gradient.SetKeys(colorKeys, alphaKeys);
            line.colorGradient = gradient;
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

                // Above Ground, where the node art draws, and below Villagers,
                // so a route lies on the board without covering a unit. The
                // lowest layer sits under Ground and would hide the routes
                // behind the nodes entirely.
                //
                // Resolved by name so the layer can be changed in the Inspector,
                // and falls back to the lowest layer if the name is not defined
                // rather than silently landing on Default.
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
