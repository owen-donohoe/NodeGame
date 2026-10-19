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
        private const float ShotDuration = 0.35f;
        private const float MarketX = -13f;
        public const float ShopStallSpacing = 6f;
        private const float MarketZ = 9f;
        public Camera ShopCamera => stageCamera;
        public float ShopReferenceDistance => MarketFrame().ReferenceDistance;
        private ShopMarketFrame marketFrame;
        private float marketAspect;
        private const int StageLayer = 2; // Ignore Raycast; picking includes it explicitly.
        private static readonly Color Sea = new Color(0.42f, 0.61f, 0.72f);

        // ItemTint's palette, from Assets/UI/Styles/Tokens.uss (--pal-tile-0 .. -7).
        private static readonly Color32[] Tints =
        {
            new Color32(58, 110, 108, 255), new Color32(160, 82, 45, 255),
            new Color32(72, 82, 140, 255), new Color32(104, 112, 52, 255),
            new Color32(112, 64, 112, 255), new Color32(78, 92, 104, 255),
            new Color32(158, 122, 40, 255), new Color32(140, 62, 62, 255)
        };

        private StagePaperArt paper;
        private readonly List<Material> materials = new List<Material>();
        private readonly Transform[] plots = new Transform[StageSlots.Count];
        private readonly GameObject[] districts = new GameObject[StageSlots.Count];
        private readonly Vector3[] slotPositions = new Vector3[StageSlots.Count];
        private readonly Vector3[] slotDirections = new Vector3[StageSlots.Count];
        private readonly GameObject[] outlines = new GameObject[StageSlots.Count];
        private readonly float[] separations = new float[StageSlots.Count];
        private readonly Transform[] marketmen = new Transform[5];
        private readonly GameObject[] marketStalls = new GameObject[5];
        private Transform[] suits = Array.Empty<Transform>();
        private Vector3[] suitTargets = Array.Empty<Vector3>();
        private float[] suitWaits = Array.Empty<float>();
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
        private float shotElapsed = ShotDuration;

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
            Primitive("Land", PrimitiveType.Plane, transform, new Vector3(0f, -0.01f, 0f),
                new Vector3(1f, 1f, 0.7f), new Color(0.77f, 0.70f, 0.55f));

            var slots = StageSlots.Layout(0);
            for (int i = 0; i < slots.Length; i++)
            {
                StageSlot slot = slots[i];
                slotPositions[i] = new Vector3((slot.Column - (slot.Row == 0 ? 1f : 0.5f)) * 2.8f,
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
                    new Vector3(0f, 0.03f, 0.35f), new Vector2(1.9f, 1.65f), Tints[0]);
                // Match centres so the enlarged backing stays farther away in the
                // transparent queue, even when the camera looks down at the facade.
                Vector3 backing = outlines[i].transform.localPosition;
                backing.y = districts[i].transform.localPosition.y;
                outlines[i].transform.localPosition = backing;
                districts[i].SetActive(false);
            }

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
                    new Vector3(0f, 0.02f, 0f), new Vector2(1.35f, 1.9f), Tints[i]);
                Cutout("Canopy", stall.transform, PaperKind.Canopy,
                    new Vector3(0f, 1.4f, -0.03f), new Vector2(1.5f, 0.75f), Tints[(i + 3) % Tints.Length]);
                marketmen[i] = Cutout("Marketman", stall.transform, PaperKind.Marketman,
                    new Vector3(-0.25f, 0.02f, -0.08f), new Vector2(0.9f, 1.65f),
                    Tints[(i + 1) % Tints.Length]).transform;
            }
            var reserved = new GameObject("Reserved ground - Social and Guild");
            reserved.transform.SetParent(transform, false);
            reserved.transform.localPosition = new Vector3(10f, 0f, 0f);

            GetShot(StageShot.Home, out shotPosition, out shotRotation);
            stageCamera.transform.localPosition = shotPosition;
            stageCamera.transform.localRotation = shotRotation;
        }

        private Rect focusRect;

        /// <summary>Screen pixels, bottom-left origin. The camera still renders the full screen.</summary>
        public void SetFocusRect(Rect screenRect)
        {
            focusRect = screenRect;
            UpdateFocusProjection();
        }

        public void ClearFocusRect()
        {
            focusRect = default;
            stageCamera.ResetProjectionMatrix();
        }

        private void LateUpdate()
        {
            UpdateMarketVisibility();
            BillboardFigures(suits);
            BillboardFigures(marketmen);
            UpdateFocusProjection();
        }

        private void UpdateMarketVisibility()
        {
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
            }
        }

        private void UpdateFocusProjection()
        {
            stageCamera.ResetProjectionMatrix();
            if (focusRect.width <= 0f || focusRect.height <= 0f || Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < slotPositions.Length; i++)
                for (int corner = 0; corner < 8; corner++)
                {
                    // Reserve the separated state too, so selection never changes the zoom.
                    Vector3 offset = new Vector3((corner & 1) == 0 ? -2.1f : 2.1f,
                        (corner & 2) == 0 ? 0f : 1.6f, (corner & 4) == 0 ? -2.1f : 2.1f);
                    Vector3 point = stageCamera.WorldToViewportPoint(transform.TransformPoint(slotPositions[i] + offset));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            Vector2 size = max - min;
            float scale = Mathf.Min(focusRect.width / Screen.width / Mathf.Max(0.001f, size.x),
                focusRect.height / Screen.height / Mathf.Max(0.001f, size.y)) * 0.9f;
            Vector2 desired = new Vector2(focusRect.center.x / Screen.width, focusRect.center.y / Screen.height);
            Matrix4x4 projection = stageCamera.projectionMatrix;
            projection.m00 *= scale;
            projection.m11 *= scale;
            projection.m02 += (min.x + max.x - 1f) * scale - (desired.x * 2f - 1f);
            projection.m12 += (min.y + max.y - 1f) * scale - (desired.y * 2f - 1f);
            stageCamera.projectionMatrix = projection;
        }

        public void ShowShot(StageShot shot)
        {
            if (shot == StageShot.Market) { SetShopScroll(0f); return; }
            shotStartPosition = stageCamera.transform.localPosition;
            shotStartRotation = stageCamera.transform.localRotation;
            GetShot(shot, out shotPosition, out shotRotation);
            shotElapsed = 0f;
        }

        /// <summary>Continuous stall units. Applies exactly, without a camera tween.</summary>
        public void SetShopScroll(float position)
        {
            GetShot(StageShot.Market, out shotPosition, out shotRotation);
            shotPosition.z -= Mathf.Clamp(position, 0f, 4f) * ShopStallSpacing;
            shotElapsed = ShotDuration;
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
                case StageShot.Social:
                    target = new Vector3(8f, 0f, 0f);
                    position = target + new Vector3(0f, 9f, -12f);
                    break;
                case StageShot.Guild:
                    target = new Vector3(12f, 0f, 0f);
                    position = target + new Vector3(0f, 9f, -12f);
                    break;
                default:
                    target = new Vector3(0f, 0f, 0f);
                    // Leave room for the separated cluster at the lobby's 390:844 portrait aspect.
                    position = new Vector3(0f, shot == StageShot.Loadout ? 17.5f : 18f, -26f);
                    break;
            }
            rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        public void SetLoadout(int[] districtTypes, int[] suitTypes)
        {
            equipped = Mathf.Min(districtTypes == null ? 0 : districtTypes.Length, StageSlots.Count);
            cluster = new DistrictCluster(equipped);
            for (int i = 0; i < districts.Length; i++)
            {
                outlines[i].SetActive(false);
                districts[i].SetActive(i < equipped);
                if (i < equipped)
                {
                    PaperKind kind = DistrictPaper(districtTypes[i]);
                    Color tint = TypeTint(districtTypes[i], "district_");
                    districts[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(paper.TextureFor(kind, tint), Color.white);
                    outlines[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(paper.TextureFor(kind, Color.white, true), StagePaperArt.Ink);
                }
            }
            for (int i = 0; i < suits.Length; i++)
            {
                suits[i].gameObject.SetActive(false);
                Destroy(suits[i].gameObject);
            }
            suits = new Transform[suitTypes == null ? 0 : suitTypes.Length];
            suitTargets = new Vector3[suits.Length];
            suitWaits = new float[suits.Length];
            focusedSuit = -1;
            for (int i = 0; i < suits.Length; i++)
            {
                var suit = Cutout("Suit " + i, plots[1], PaperKind.Villager,
                    new Vector3((i % 3 - 1) * 0.5f, 0.03f, -0.55f),
                    new Vector2(0.7f, 1.1f), TypeTint(suitTypes[i], "suit_"));
                suits[i] = suit.transform;
                suitTargets[i] = suit.transform.localPosition;
                suitWaits[i] = UnityEngine.Random.Range(0.2f, 1.5f);
            }
        }

        public void BeginSuitReplace() => cluster.Use(false);

        public bool TryPick(Vector2 screenPoint, out StagePick pick)
        {
            pick = default;
            if (!stageCamera.isActiveAndEnabled) return false;
            Physics.SyncTransforms();
            Ray ray = stageCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, stageCamera.farClipPlane,
                1 << StageLayer, QueryTriggerInteraction.Ignore)) return false;
            for (int i = 0; i < suits.Length; i++)
                if (hit.transform == suits[i]) { pick = new StagePick(true, i); return true; }
            for (int i = 0; i < plots.Length; i++)
                if (hit.transform.IsChildOf(plots[i])) { pick = new StagePick(false, i); return true; }
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

        public void CancelReplace() => cluster.Cancel();

        public void FocusSuit(int index)
        {
            if (index >= 0 && index < suits.Length) focusedSuit = index;
        }

        public void ClearFocus() => focusedSuit = -1;

        public void SetVisible(bool visible)
        {
            stageCamera.enabled = visible;
            gameObject.SetActive(visible);
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (shotElapsed < ShotDuration)
            {
                shotElapsed = Mathf.Min(ShotDuration, shotElapsed + delta);
                float t = 1f - Mathf.Pow(1f - shotElapsed / ShotDuration, 3f);
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
                if (cluster.Shaking)
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
            for (int i = 0; i < suits.Length; i++)
            {
                if (i == focusedSuit) continue;
                Vector3 position = suits[i].localPosition;
                Vector3 direction = suitTargets[i] - position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    suits[i].localPosition = Vector3.MoveTowards(position, suitTargets[i], delta * 0.35f);
                }
                else
                {
                    suitWaits[i] -= delta;
                    if (suitWaits[i] > 0f) continue;
                    // Walk on the front of the middle-top plot, clear of the filler building.
                    suitTargets[i] = new Vector3(UnityEngine.Random.Range(-1f, 1f), position.y,
                        UnityEngine.Random.Range(-1f, -0.3f));
                    suitWaits[i] = UnityEngine.Random.Range(0.5f, 2f);
                }
            }
        }

private static PaperKind DistrictPaper(int type)
            => (PaperKind)((type % 3 + 3) % 3);

        private static Color TypeTint(int type, string prefix)
            => Tints[ItemTint.IndexFor(prefix + type.ToString(System.Globalization.CultureInfo.InvariantCulture))];

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
