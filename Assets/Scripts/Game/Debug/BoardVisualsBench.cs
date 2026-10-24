using System.Collections.Generic;
using NodeWar.Config;
using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>GFX Testing scene harness; never constructs or mutates simulation state.</summary>
    public sealed class BoardVisualsBench : MonoBehaviour
    {
        [SerializeField] private BoardConfig boardConfig;
        [Tooltip("Multiplier on BoardConfig node spacing; 1 matches the game.")]
        [SerializeField] private float nodeScale = 1f;
        [SerializeField, Range(-1, 3)] private int tierOverride = -1;
        [SerializeField] private float stepSeconds = 0.8f;
        [SerializeField] private bool autoReplay = true;
        [SerializeField] private bool frameCamera = true;

        private BoardConfig runtimeConfig;
        private BoardTerrainView view;
        private DraftPlacement[] sample;
        private readonly List<DraftPlacement> placed = new List<DraftPlacement>();
        private bool[] occupied;
        private int next;
        private float elapsed;
        private bool matched;

        private void Start() => ReplayDraft();

        private void OnValidate() => ApplyTier();

        private void ApplyTier()
        {
            if (WaterQuality.Override == tierOverride) return;
            WaterQuality.Override = tierOverride;
            WaterSurfaceView.RefreshAll();
        }

        [ContextMenu("Replay draft")]
        public void ReplayDraft()
        {
            if (!Application.isPlaying) return;
            ReleaseBoard();
            ApplyTier();
            if (boardConfig == null)
            {
                Debug.LogWarning("BoardVisualsBench needs a BoardConfig.", this);
                return;
            }
            // Match GameManager's Create(config) path without editing the shared config asset.
            runtimeConfig = Instantiate(boardConfig);
            runtimeConfig.nodeScale = boardConfig.nodeScale * Mathf.Max(0.01f, nodeScale);
            view = BoardTerrainView.Create(runtimeConfig);
            view.transform.SetParent(transform, true);
            sample = BoardVisualsBenchDraft.Create();
            occupied = new bool[runtimeConfig.Data.gridCols * runtimeConfig.Data.gridRows];
            placed.Clear();
            next = 0;
            elapsed = 0f;
            matched = false;
            ShowDraft();
            if (frameCamera) FrameCamera();
        }

        private void Update()
        {
            ApplyTier();
            if (view == null) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < Mathf.Max(0.05f, stepSeconds)) return;
            elapsed = 0f;
            if (matched)
            {
                if (autoReplay) ReplayDraft();
                return;
            }
            if (next >= sample.Length)
            {
                view.ShowMatch(placed.ToArray());
                matched = true;
                // Leave time to inspect the completed shoal rise before looping.
                elapsed = -Mathf.Max(0.8f, stepSeconds * 2f);
                return;
            }
            DraftPlacement placement = sample[next++];
            BoardConfigData board = runtimeConfig.Data;
            if (PlacementLegality.CanPlace(board, occupied, placement.districtType, placement.gridX, placement.gridZ))
            {
                occupied[placement.gridZ * board.gridCols + placement.gridX] = true;
                placed.Add(placement);
            }
            else Debug.LogWarning($"Skipping illegal bench placement: {placement.districtType} at ({placement.gridX},{placement.gridZ}).", this);
            ShowDraft();
        }

        private void ShowDraft()
        {
            view.ShowDraft(placed.ToArray(), next < sample.Length,
                next < sample.Length ? sample[next].districtType : DistrictType.None);
        }

        private void FrameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            BoardConfigData board = runtimeConfig.Data;
            float scale = runtimeConfig.nodeScale;
            Vector3 centre = new Vector3((board.gridCols - 1) * scale * 0.5f, 0f, (board.gridRows - 1) * scale * 0.5f);
            float radius = Mathf.Sqrt(board.gridCols * board.gridCols + board.gridRows * board.gridRows) * scale * 0.5f;
            camera.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
            float halfFov = camera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            float limitingFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * camera.aspect));
            float distance = radius * 1.15f / Mathf.Sin(limitingFov);
            camera.transform.position = centre - camera.transform.forward * distance;
            if (camera.orthographic) camera.orthographicSize = radius * 1.15f / Mathf.Min(1f, camera.aspect);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + radius * 2f);
        }

        private void ReleaseBoard()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            if (runtimeConfig != null) Destroy(runtimeConfig);
            view = null;
            runtimeConfig = null;
        }

        private void OnDisable()
        {
            if (WaterQuality.Override != tierOverride) return;
            WaterQuality.Override = -1;
            WaterSurfaceView.RefreshAll();
        }

        private void OnDestroy() => ReleaseBoard();
    }
}
