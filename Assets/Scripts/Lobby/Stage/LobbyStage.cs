using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NodeWar.View;

namespace NodeWar.Lobby
{
    /// <summary>Runtime-only lobby scenery. Its owner supplies loadouts and page changes.</summary>
    public sealed class LobbyStage : MonoBehaviour
    {
        private const float MarketX = -13f;
        public const float ShopStallSpacing = 6f;
        private const float MarketZ = 9f;
        public Camera ShopCamera => stageCamera;
        public float ShopReferenceDistance => MarketFrame().ReferenceDistance;
        private ShopMarketFrame marketFrame;
        private float marketAspect;
        private const int StageLayer = 2; // Ignore Raycast; picking includes it explicitly.
        private static readonly Color Sea = new Color(0.42f, 0.61f, 0.72f);


        private StagePaperArt paper;
        private readonly List<Material> materials = new List<Material>();
        private readonly Transform[] plots = new Transform[StageSlots.Count];
        private readonly GameObject[] districts = new GameObject[StageSlots.Count];
        private readonly Vector3[] slotPositions = new Vector3[StageSlots.Count];
        private readonly Vector3[] slotDirections = new Vector3[StageSlots.Count];
        private readonly GameObject[] outlines = new GameObject[StageSlots.Count];
        private readonly Renderer[] clusterRenderers = new Renderer[StageSlots.Count * 3];
        private Renderer[] suitRenderers = Array.Empty<Renderer>();
        private Bounds clusterEnvelope;
        private float workshopTop = 0.21f, workshopBottom = 0.55f, workshopAspect;
        private readonly float[] separations = new float[StageSlots.Count];
        private readonly Transform[] marketmen = new Transform[5];
        private readonly GameObject[] marketStalls = new GameObject[5];
        private Transform[] suits = Array.Empty<Transform>();
        private Vector3[] suitTargets = Array.Empty<Vector3>();
        private float[] suitWaits = Array.Empty<float>();
        private int[] suitIdentities = Array.Empty<int>();
        private Vector3[] suitGround = Array.Empty<Vector3>();
        private float[] suitLifts = Array.Empty<float>(), suitAngles = Array.Empty<float>(), suitPops = Array.Empty<float>();
        private bool replacingSuits, loweringSuits;
        private float suitMotionTime;
        private float[] lowerLifts = Array.Empty<float>(), lowerAngles = Array.Empty<float>();
        private int focusedSuit = -1;
        private Camera stageCamera;
        private Shader shader;
        private DistrictCluster cluster = new DistrictCluster(0);
        private int equipped;
        private int districtCapacity = StageSlots.Count;
        private Vector3 shotStartPosition;
        private Quaternion shotStartRotation;
        private Vector3 shotPosition;
        private Quaternion shotRotation;
        private double shotStarted = double.NegativeInfinity;
        private bool pageDriven, pageMoving;
        public bool IsPanning => pageMoving || Time.unscaledTimeAsDouble - shotStarted < LobbyMotion.PageSeconds;
        private StageShot currentShot = StageShot.Home;
        private float shopScroll;

        /// <summary>
        /// FillEmpty/ Replaced asks the owner to apply its chosen type, then call SetLoadout.
        /// This preview never writes inventory. FillEmpty's Slot is the first empty slot.
        /// </summary>
        public event Action<DistrictClusterResult> DistrictUsed;

        public static LobbyStage Create()
        {
            var root = new GameObject("Lobby Stage");
            // Keep the stage away from the scene camera and other scene geometry.
            root.transform.position = new Vector3(0f, -10000f, 0f);
            var stage = root.AddComponent<LobbyStage>();
            stage.Build();
            return stage;
        }

        private void Build()
        {
            paper = new StagePaperArt(Mathf.Max(Screen.width, Screen.height));
            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var cameraObject = new GameObject("Stage Camera");
            cameraObject.transform.SetParent(transform, false);
            stageCamera = cameraObject.AddComponent<Camera>();
            // Lobby.unity's base camera has depth -1. Screen-space UI Toolkit renders
            // after cameras; LobbyPanelSettings clears depth, but does not clear colour.
            float depth = 0f;
            foreach (Camera other in Camera.allCameras)
                if (other != stageCamera && other.targetTexture == null)
                    depth = Mathf.Max(depth, other.depth + 1f);
            stageCamera.depth = depth;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = Sea;
            stageCamera.cullingMask = 1 << StageLayer;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 120f;
            stageCamera.fieldOfView = ShopMarketFraming.FieldOfView;
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;
            stageCamera.transparencySortMode = TransparencySortMode.Perspective;
            // TODO: add the URP Volume depth-of-field hook during the visual pass.

            Primitive("Ocean (unplaceable edge)", PrimitiveType.Plane, transform,
                new Vector3(0f, -0.18f, 0f), new Vector3(12f, 1f, 12f), Sea);

            var slots = StageSlots.Layout(0);
            for (int i = 0; i < slots.Length; i++)
            {
                StageSlot slot = slots[i];
                slotPositions[i] = new Vector3((slot.Column - (slot.Row == 1 ? 1f : 0.5f)) * 2.8f,
                    0f, slot.Row == 0 ? 1.4f : -1.4f);
                slotDirections[i] = slotPositions[i].normalized;
                var plot = new GameObject("Plot " + i);
                plot.transform.SetParent(transform, false);
                plot.transform.localPosition = slotPositions[i];
                plots[i] = plot.transform;
                var ground = Primitive("Unclaimed ground", PrimitiveType.Quad, plots[i],
                    new Vector3(0f, 0.02f, 0f), new Vector3(2.78f, 2.78f, 1f), PlayerColors.Neutral);
                ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                outlines[i] = Cutout("Selection ink", plots[i], PaperKind.House,
                    new Vector3(0f, 0.03f, 0.38f), new Vector2(2.06f, 1.79f), Color.white, true);
                outlines[i].SetActive(false);
                districts[i] = Cutout("District", plots[i], PaperKind.House,
                    new Vector3(0f, 0.03f, 0.35f), new Vector2(1.9f, 1.65f), StagePaperArt.Tints[0]);
                // Match centres so the enlarged backing stays farther away in the
                // transparent queue, even when the camera looks down at the facade.
                Vector3 backing = outlines[i].transform.localPosition;
                backing.y = districts[i].transform.localPosition.y;
                outlines[i].transform.localPosition = backing;
                clusterRenderers[i * 3] = ground.GetComponent<Renderer>();
                clusterRenderers[i * 3 + 1] = districts[i].GetComponent<Renderer>();
                clusterRenderers[i * 3 + 2] = outlines[i].GetComponent<Renderer>();
                districts[i].SetActive(false);
            }

            CacheClusterEnvelope();

            // The market (about 15 textures) builds on first Shop visit; see EnsureMarket.
            var reserved = new GameObject("Reserved ground - Social and Guild");
            reserved.transform.SetParent(transform, false);
            reserved.transform.localPosition = new Vector3(10f, 0f, 0f);

            GetShot(StageShot.Home, out shotPosition, out shotRotation);
            stageCamera.transform.localPosition = shotPosition;
            stageCamera.transform.localRotation = shotRotation;
        }

        private bool marketBuilt;

        /// <summary>Builds stalls, canopies and marketmen on the first Shop visit or scroll.</summary>
        private void EnsureMarket()
        {
            if (marketBuilt) return;
            marketBuilt = true;
            var market = new GameObject("Market node");
            market.transform.SetParent(transform, false);
            market.transform.localPosition = new Vector3(MarketX, 0f, MarketZ);
            Primitive("Market ground", PrimitiveType.Plane, market.transform,
                new Vector3(0f, -0.01f, -12f), new Vector3(0.36f, 1f, 3f),
                new Color(0.77f, 0.70f, 0.55f));
            string[] names = { "Daily", "Crates", "Weekly skins", "Bundle", "Bank" };
            for (int i = 0; i < names.Length; i++)
            {
                var stall = new GameObject(names[i] + " stall");
                marketStalls[i] = stall;
                stall.transform.SetParent(market.transform, false);
                stall.transform.localPosition = new Vector3(ShopMarketFraming.StallX, 0f, -i * ShopStallSpacing);
                Cutout("Stall facade", stall.transform, PaperKind.Stall,
                    new Vector3(0f, 0.02f, 0f), new Vector2(1.35f, 1.9f), StagePaperArt.Tints[i]);
                Cutout("Canopy", stall.transform, PaperKind.Canopy,
                    new Vector3(0f, 1.4f, -0.03f), new Vector2(1.5f, 0.75f), StagePaperArt.Tints[(i + 3) % StagePaperArt.Tints.Length]);
                marketmen[i] = Cutout("Marketman", stall.transform, PaperKind.Marketman,
                    new Vector3(-0.25f, 0.02f, -0.08f), new Vector2(0.9f, 1.65f),
                    StagePaperArt.Tints[(i + 1) % StagePaperArt.Tints.Length]).transform;
            }
        }

        private void LateUpdate()
        {
            UpdateMarketVisibility();
            BillboardFigures(suits);
            BillboardFigures(marketmen);
        }

        private void UpdateMarketVisibility()
        {
            if (!marketBuilt) return;
            Vector3 cameraPosition = stageCamera.transform.position;
            for (int i = 0; i < marketStalls.Length; i++)
            {
                GameObject stall = marketStalls[i];
                Vector3 centre = stall.transform.position + Vector3.up;
                bool visible = ShopMarketFraming.ShouldRenderStall((centre - cameraPosition).sqrMagnitude);
                if (stall.activeSelf != visible) stall.SetActive(visible);
            }
        }

        private void BillboardFigures(Transform[] figures)
        {
            for (int i = 0; i < figures.Length; i++)
            {
                Transform figure = figures[i];
                if (figure == null || !figure.gameObject.activeInHierarchy) continue;
                // Keep feet on the ground. Camera pitch never tips a paper figure.
                Vector3 forward = figure.position - stageCamera.transform.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    figure.rotation = Quaternion.LookRotation(forward, Vector3.up);
                if (figures == suits) figure.rotation *= Quaternion.Euler(0, 0, suitAngles[i]);
            }
        }

        public void ShowShot(StageShot shot)
        {
            if (shot == StageShot.Market) EnsureMarket();
            pageDriven = pageMoving = false;
            currentShot = shot;
            shotStartPosition = stageCamera.transform.localPosition;
            shotStartRotation = stageCamera.transform.localRotation;
            GetShot(shot, out shotPosition, out shotRotation);
            if (shot == StageShot.Market) shotPosition.z -= shopScroll * ShopStallSpacing;
            shotStarted = Time.unscaledTimeAsDouble;
        }

        /// <summary>Sampled by the shell from the same coordinate as the page strip.</summary>
        public void ApplyPageMotion(StageShot from, StageShot to, float progress, bool moving)
        {
            if (from == StageShot.Market || to == StageShot.Market) EnsureMarket();
            pageDriven = true; pageMoving = moving;
            currentShot = to;
            GetShot(from, out var a, out var ar);
            GetShot(to, out var b, out var br);
            if (from == StageShot.Market) a.z -= shopScroll * ShopStallSpacing;
            if (to == StageShot.Market) b.z -= shopScroll * ShopStallSpacing;
            shotPosition = Vector3.LerpUnclamped(a, b, progress);
            shotRotation = Quaternion.SlerpUnclamped(ar, br, progress);
            stageCamera.transform.localPosition = shotPosition;
            stageCamera.transform.localRotation = shotRotation;
            shotStarted = double.NegativeInfinity;
        }

        /// <summary>Continuous stall units. Applies exactly, without a camera tween.</summary>
        public void SetShopScroll(float position)
        {
            EnsureMarket();
            shopScroll = Mathf.Clamp(position, 0f, 4f);
            if (currentShot != StageShot.Market) return;
            GetShot(StageShot.Market, out shotPosition, out shotRotation);
            shotPosition.z -= shopScroll * ShopStallSpacing;
            if (IsPanning) return;
            stageCamera.transform.localPosition = shotPosition;
            stageCamera.transform.localRotation = shotRotation;
        }

        /// <summary>Right of the counter as seen from the market camera, looking down +Z.</summary>
        public Vector3 ShopAnchor(int stall) => transform.TransformPoint(
            new Vector3(MarketX + MarketFrame().AnchorX, 1f, MarketZ - Mathf.Clamp(stall, 0, 4) * ShopStallSpacing));
        private ShopMarketFrame MarketFrame()
        {
            if (!Mathf.Approximately(marketAspect, stageCamera.aspect))
            {
                marketAspect = stageCamera.aspect;
                marketFrame = ShopMarketFraming.ForAspect(marketAspect);
            }
            return marketFrame;
        }

        private void GetShot(StageShot shot, out Vector3 position, out Quaternion rotation)
        {
            Vector3 target;
            switch (shot)
            {
                case StageShot.Market:
                    ShopMarketFrame frame = MarketFrame();
                    target = new Vector3(MarketX + frame.CameraX, 1f, MarketZ);
                    position = target + new Vector3(0f, ShopMarketFraming.CameraRise, -frame.CameraBack);
                    break;
                case StageShot.Ocean:
                    target = new Vector3(18f, 0f, 0f);
                    position = target + new Vector3(0f, 9f, -12f);
                    break;
                case StageShot.Districts:
                case StageShot.Suits:
                    var envelope = shot == StageShot.Suits ? SuitEnvelope() : clusterEnvelope;
                    var half = envelope.extents;
                    var fit = StageClusterFraming.Fit(stageCamera.aspect,
                        shot == StageShot.Suits ? StageClusterFraming.SuitTop(workshopTop, workshopBottom, focusedSuit >= 0 || replacingSuits) : workshopTop,
                        shot == StageShot.Suits ? StageClusterFraming.SuitBottom(workshopTop, workshopBottom, focusedSuit >= 0 || replacingSuits) : workshopBottom,
                        shot == StageShot.Suits, half.x, half.y, half.z);
                    rotation = Quaternion.Euler(fit.Pitch, 0f, 0f);
                    position = envelope.center - rotation * Vector3.forward * fit.Distance - rotation * Vector3.up * fit.UpOffset;
                    return;
                default:
                    target = new Vector3(0f, -1.5f, 0f);
                    // Leave room for the separated cluster at the lobby's 390:844 portrait aspect.
                    position = new Vector3(0f, 18f, -26f);
                    break;
            }
            rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        private static Vector3 Corner(Bounds bounds, int index) => new Vector3(
            (index & 1) == 0 ? bounds.min.x : bounds.max.x,
            (index & 2) == 0 ? bounds.min.y : bounds.max.y,
            (index & 4) == 0 ? bounds.min.z : bounds.max.z);

        private Bounds SuitEnvelope()
        {
            if (suitRenderers.Length == 0) return new Bounds(plots[1].localPosition + Vector3.up, Vector3.one);
            bool first = true;
            var bounds = new Bounds();
            for (int i = 0; i < suitRenderers.Length; i++)
            {
                var renderer = suitRenderers[i];
                Vector3 min = Vector3.Scale(renderer.localBounds.min, new Vector3(0.7f, 1.1f, 1f));
                Vector3 max = Vector3.Scale(renderer.localBounds.max, new Vector3(0.7f, 1.1f, 1f));
                var sweep = StageClusterFraming.SweepSuit(min.x, max.x, min.y, max.y, min.z, max.z);
                var body = new Bounds(slotPositions[1] + new Vector3(0, 0.03f + 1.1f * 0.42f + sweep.CentreY, sweep.CentreZ),
                    new Vector3(sweep.HalfWidth, sweep.HalfHeight, sweep.HalfDepth) * 2);
                if (first) { bounds = body; first = false; }
                else bounds.Encapsulate(body);
            }
            return bounds;
        }

        private void CacheClusterEnvelope()
        {
            bool first = true;
            foreach (var renderer in clusterRenderers)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(Corner(renderer.localBounds, i)));
                    if (first) { clusterEnvelope = new Bounds(point, Vector3.zero); first = false; }
                    else clusterEnvelope.Encapsulate(point);
                }
            // Selection separates plots by 0.65 and replacement jitters by at most 0.025.
            clusterEnvelope.Expand(new Vector3(1.4f, 0f, 1.4f));
        }

        public void SetWorkshopBand(float top, float bottom)
        {
            if (Mathf.Approximately(top, workshopTop) && Mathf.Approximately(bottom, workshopBottom) &&
                Mathf.Approximately(stageCamera.aspect, workshopAspect)) return;
            workshopTop = top; workshopBottom = bottom; workshopAspect = stageCamera.aspect;
            if (currentShot == StageShot.Districts || currentShot == StageShot.Suits)
            {
                if (IsPanning) GetShot(currentShot, out shotPosition, out shotRotation);
                else ShowShot(currentShot);
            }
        }

        /// <summary>Current cluster bounds in bottom-origin viewport coordinates, including empty plots and suits.</summary>
        public bool TryGetClusterViewportBounds(out Rect bounds)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool valid = true;
            void Include(Renderer renderer)
            {
                if (!renderer.gameObject.activeInHierarchy) return;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = stageCamera.WorldToViewportPoint(Corner(renderer.bounds, i));
                    if (point.z <= stageCamera.nearClipPlane) { valid = false; continue; }
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            foreach (var renderer in clusterRenderers) Include(renderer);
            foreach (var renderer in suitRenderers) Include(renderer);
            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return valid && !float.IsInfinity(min.y);
        }
        private readonly bool[] lockedDistrictSlots = new bool[StageSlots.Count];
        private readonly bool[] lockedSuitSlots = new bool[3];

        public void SetLoadout(int[] districtTypes, int[] suitTypes, int[] grantedDistricts = null,
            int[] lockedDistricts = null, int[] lockedSuits = null)
        {
            for (int i = 0; i < lockedDistrictSlots.Length; i++)
                lockedDistrictSlots[i] = districtTypes != null && i < districtTypes.Length &&
                    Array.IndexOf(lockedDistricts ?? Array.Empty<int>(), districtTypes[i]) >= 0;
            for (int i = 0; i < lockedSuitSlots.Length; i++)
                lockedSuitSlots[i] = suitTypes != null && i < suitTypes.Length &&
                    Array.IndexOf(lockedSuits ?? Array.Empty<int>(), suitTypes[i]) >= 0;
            equipped = Mathf.Min(districtTypes == null ? 0 : districtTypes.Length, StageSlots.Count);
            cluster = new DistrictCluster(equipped);
            // Granted districts take the last free slots, after the chosen ones, and are never picked.
            var slotTypes = new int[StageSlots.Count];
            for (int i = 0; i < equipped; i++) slotTypes[i] = districtTypes[i];
            var extra = new List<int>();
            if (grantedDistricts != null)
                foreach (int type in grantedDistricts)
                    if (Array.IndexOf(districtTypes ?? Array.Empty<int>(), type) < 0 && !extra.Contains(type)) extra.Add(type);
            int firstGranted = Mathf.Max(equipped, StageSlots.Count - extra.Count);
            for (int k = 0; k < extra.Count && firstGranted + k < StageSlots.Count; k++) slotTypes[firstGranted + k] = extra[k];
            for (int i = 0; i < districts.Length; i++)
            {
                outlines[i].SetActive(false);
                bool shown = slotTypes[i] > 0;
                districts[i].SetActive(shown);
                if (shown)
                {
                    PaperKind kind = StageArt.District(slotTypes[i]);
                    Color tint = TypeTint(slotTypes[i], false);
                    districts[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(paper.TextureFor(kind, tint, mark: slotTypes[i]), Color.white);
                    outlines[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(paper.TextureFor(kind, Color.white, true), StagePaperArt.Ink);
                }
            }
            if (StageLoadoutTypes.Same(suitIdentities, suitTypes ?? Array.Empty<int>())) return;
            var oldSuits = suits; var oldIds = suitIdentities;
            var oldGround = suitGround; var oldTargets = suitTargets; var oldWaits = suitWaits;
            var oldLifts = suitLifts; var oldAngles = suitAngles; var oldPops = suitPops;
            var oldLowerLifts = lowerLifts; var oldLowerAngles = lowerAngles;
            suits = new Transform[suitTypes == null ? 0 : suitTypes.Length];
            suitIdentities = suitTypes == null ? Array.Empty<int>() : (int[])suitTypes.Clone();
            suitRenderers = new Renderer[suits.Length];
            suitTargets = new Vector3[suits.Length];
            suitGround = new Vector3[suits.Length];
            suitWaits = new float[suits.Length];
            suitLifts = new float[suits.Length]; suitAngles = new float[suits.Length]; suitPops = new float[suits.Length];
            lowerLifts = new float[suits.Length]; lowerAngles = new float[suits.Length];
            var retained = new bool[oldSuits.Length];
            for (int i = 0; i < suits.Length; i++)
            {
                int previous = i < oldIds.Length && oldIds[i] == suitTypes[i] ? i : -1;
                if (previous >= 0 && !retained[previous])
                {
                    retained[previous] = true; suits[i] = oldSuits[previous];
                    suitGround[i] = oldGround[previous]; suitTargets[i] = oldTargets[previous]; suitWaits[i] = oldWaits[previous];
                    suitLifts[i] = oldLifts[previous]; suitAngles[i] = oldAngles[previous]; suitPops[i] = oldPops[previous];
                    lowerLifts[i] = oldLowerLifts[previous]; lowerAngles[i] = oldLowerAngles[previous];
                }
                else
                {
                    var suit = Cutout("Suit " + i, plots[1], StageArt.Suit(suitTypes[i]),
                        new Vector3(SuitReplacementMotion.LaneX(i, suits.Length), 0.03f, -0.55f),
                        new Vector2(0.7f, 1.1f), TypeTint(suitTypes[i], true));
                    suits[i] = suit.transform;
                    suitGround[i] = i < oldGround.Length ? oldGround[i] : suit.transform.localPosition;
                    suitTargets[i] = suitGround[i]; suitWaits[i] = 0.5f;
                    suitPops[i] = loweringSuits ? 0f : 1f;
                    if (i < oldLifts.Length)
                    {
                        suitLifts[i] = oldLifts[i]; suitAngles[i] = oldAngles[i];
                        lowerLifts[i] = oldLowerLifts[i]; lowerAngles[i] = oldLowerAngles[i];
                    }
                    suit.GetComponent<Renderer>().sharedMaterial = MaterialFor(
                        paper.TextureFor(StageArt.Suit(suitTypes[i]), TypeTint(suitTypes[i], true), mark: suitTypes[i]), Color.white);
                }
                suits[i].name = "Suit " + i;
                if (oldSuits.Length != suits.Length)
                {
                    suitGround[i].x = SuitReplacementMotion.LaneX(i, suits.Length);
                    suitTargets[i].x = suitGround[i].x;
                }
                suitRenderers[i] = suits[i].GetComponent<Renderer>();
                suits[i].localPosition = suitGround[i] + Vector3.up * suitLifts[i];
            }
            for (int i = 0; i < oldSuits.Length; i++)
                if (!retained[i]) { oldSuits[i].gameObject.SetActive(false); Destroy(oldSuits[i].gameObject); }
            if (focusedSuit >= suits.Length) focusedSuit = -1;
        }

        public void BeginSuitReplace()
        {
            replacingSuits = true; loweringSuits = false; suitMotionTime = 0;
            cluster.Cancel(); ClearFocus(); ShowShot(StageShot.Suits);
        }

        public bool TryPick(Vector2 screenPoint, out StagePick pick)
        {
            pick = default;
            if (!stageCamera.isActiveAndEnabled) return false;
            Physics.SyncTransforms();
            Ray ray = stageCamera.ScreenPointToRay(screenPoint);
            var hits = Physics.RaycastAll(ray, stageCamera.farClipPlane, 1 << StageLayer, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                int suit = Array.IndexOf(suits, hit.transform);
                if (suit >= 0)
                {
                    var candidate = new StagePick(true, suit);
                    if (candidate.CanPick(replacingSuits || cluster.Shaking, replacingSuits))
                    { pick = candidate; return true; }
                    continue;
                }
                if (replacingSuits) continue;
                for (int i = 0; i < plots.Length; i++)
                {
                    if (cluster.Shaking && i >= equipped) continue;
                    if (hit.transform.IsChildOf(plots[i])) { pick = new StagePick(false, i); return true; }
                }
            }
            return false;
        }
        public void SelectDistrict(int slot) => cluster.Select(slot);
        public void Deselect() => cluster.Deselect();
        public void SetDistrictCapacity(int capacity) => districtCapacity = Mathf.Clamp(capacity, 0, StageSlots.Count);

        /// <summary>False means Use requested filling the first empty slot, without replacement mode.</summary>
        public bool BeginReplace()
        {
            DistrictClusterResult result = cluster.Use(StageSlots.HasEmpty(equipped, districtCapacity));
            if (result.Action == DistrictClusterAction.FillEmpty)
                DistrictUsed?.Invoke(new DistrictClusterResult(result.Action, equipped));
            return cluster.State == DistrictClusterState.Replacing;
        }

        public void PickReplace(int slot)
        {
            DistrictClusterResult result = cluster.Pick(slot);
            if (result.Action != DistrictClusterAction.None) DistrictUsed?.Invoke(result);
        }

        public void CancelReplace()
        {
            cluster.Cancel();
            if (!replacingSuits) return;
            replacingSuits = false; loweringSuits = true; suitMotionTime = 0;
            Array.Copy(suitLifts, lowerLifts, suits.Length);
            Array.Copy(suitAngles, lowerAngles, suits.Length);
        }

        public void FocusSuit(int index)
        {
            if (index < 0 || index >= suits.Length) return;
            focusedSuit = index;
            ShowShot(StageShot.Suits);
        }

        public void ClearFocus()
        {
            if (focusedSuit < 0) return;
            focusedSuit = -1;
            if (currentShot == StageShot.Suits) ShowShot(StageShot.Suits);
        }

        public void SetVisible(bool visible)
        {
            stageCamera.enabled = visible;
            gameObject.SetActive(visible);
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (!pageDriven && (IsPanning || stageCamera.transform.localPosition != shotPosition))
            {
                float progress = (float)(Time.unscaledTimeAsDouble - shotStarted) / LobbyMotion.PageSeconds;
                float t = LobbyMotion.OutCubic(progress);
                stageCamera.transform.localPosition = Vector3.Lerp(shotStartPosition, shotPosition, t);
                stageCamera.transform.localRotation = Quaternion.Slerp(shotStartRotation, shotRotation, t);
            }
            AnimateCluster(delta);
            AnimateSuits(delta);
        }

        private void AnimateCluster(float delta)
        {
            for (int i = 0; i < plots.Length; i++)
            {
                int target = cluster.TargetSeparation(i);
                separations[i] = Mathf.MoveTowards(separations[i], target, delta * 4f);
                Vector3 position = slotPositions[i] + slotDirections[i] * (separations[i] * 0.65f);
                if (cluster.Shaking && !lockedDistrictSlots[i])
                {
                    Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.025f;
                    position += new Vector3(jitter.x, 0f, jitter.y);
                }
                plots[i].localPosition = position;
                bool outlined = target != 0 && i < equipped;
                if (outlines[i].activeSelf != outlined) outlines[i].SetActive(outlined);
            }
        }

        private void AnimateSuits(float delta)
        {
            suitMotionTime += delta;
            for (int i = 0; i < suits.Length; i++)
            {
                float height = suitRenderers[i].localBounds.size.y * 1.1f;
                if (replacingSuits && !lockedSuitSlots[i])
                {
                    suitLifts[i] = height * SuitReplacementMotion.Lift(suitMotionTime, i, suits.Length);
                    suitAngles[i] = SuitReplacementMotion.Shake(suitMotionTime, i, suits.Length);
                }
                else if (loweringSuits)
                {
                    float lower = SuitReplacementMotion.Lower(suitMotionTime);
                    suitLifts[i] = lowerLifts[i] * lower; suitAngles[i] = lowerAngles[i] * lower;
                }
                if (!replacingSuits && !loweringSuits && i != focusedSuit) Wander(i, delta);
                suitPops[i] += delta;
                float pop = SuitReplacementMotion.Pop(suitPops[i]);
                suits[i].localScale = new Vector3(0.7f / pop, 1.1f * pop, 1f);
                // Scale about the feet rather than the centre of the quad.
                suits[i].localPosition = suitGround[i] + Vector3.up * (suitLifts[i] + height * 0.42f * (pop - 1));
            }
            if (loweringSuits && suitMotionTime >= SuitReplacementMotion.LowerSeconds) loweringSuits = false;
        }

        private void Wander(int i, float delta)
        {
            Vector3 position = suitGround[i];
            Vector3 direction = suitTargets[i] - position;
            if (direction.sqrMagnitude > 0.0001f)
                suitGround[i] = Vector3.MoveTowards(position, suitTargets[i], delta * 0.35f);
            else
            {
                suitWaits[i] -= delta;
                if (suitWaits[i] > 0f) return;
                // Walk on the front of the front-middle plot, clear of the filler building.
                suitTargets[i] = new Vector3(SuitReplacementMotion.WanderX(i, suits.Length, UnityEngine.Random.Range(-1f, 1f)), position.y,
                    UnityEngine.Random.Range(-1f, -0.3f));
                suitWaits[i] = UnityEngine.Random.Range(0.5f, 2f);
            }
        }

        private static Color TypeTint(int type, bool suit)
            => StagePaperArt.Tints[ItemTint.IndexFor(StageArt.Identity(type, suit))];

        // All stage materials are created here; the future dither shader has one entry point.
        private Material MaterialFor(Color colour) => MaterialFor(null, colour);

        private Material MaterialFor(Texture2D texture, Color colour)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i].mainTexture == texture && materials[i].color == colour) return materials[i];
            var material = new Material(shader) { color = colour, mainTexture = texture };
            material.SetFloat("_Cull", 0f); // Paper has two sides; silhouettes are baked in.
            material.SetShaderPassEnabled("ShadowCaster", false);
            if (texture != null)
            {
                // A binary clip discards subpixel coverage. Straight-alpha blending
                // retains it; Unity's transparent queue sorts these separate flats back to front.
                material.SetFloat("_AlphaClip", 0f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            materials.Add(material);
            return material;
        }

        // Position is at the paper's feet. The art reserves 8% transparent bottom padding.
        private GameObject Cutout(string objectName, Transform parent, PaperKind kind,
            Vector3 position, Vector2 size, Color tint, bool silhouette = false)
        {
            var obj = Primitive(objectName, PrimitiveType.Quad, parent,
                position + Vector3.up * (size.y * 0.42f), new Vector3(size.x, size.y, 1f), Color.white);
            obj.GetComponent<Renderer>().sharedMaterial = MaterialFor(
                paper.TextureFor(kind, tint, silhouette), silhouette ? StagePaperArt.Ink : Color.white);
            return obj;
        }
        private GameObject Primitive(string objectName, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Color colour)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = objectName;
            obj.layer = StageLayer;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            bool pickable = objectName == "Unclaimed ground" || objectName == "District" || objectName.StartsWith("Suit ", StringComparison.Ordinal);
            collider.enabled = pickable;
            if (!pickable) Destroy(collider);
            else if (type == PrimitiveType.Quad)
            {
                collider.enabled = false;
                Destroy(collider);
                // Keep picking on the same transform as the old district/suit.
                var box = obj.AddComponent<BoxCollider>();
                box.size = objectName == "Unclaimed ground" ? new Vector3(1f, 1f, 0.02f)
                    : new Vector3(0.86f, 0.9f, 0.06f);
            }
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(colour);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++) Destroy(materials[i]);
            paper?.Dispose();
        }
    }
}
