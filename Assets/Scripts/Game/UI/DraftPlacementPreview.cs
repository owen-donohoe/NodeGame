using UnityEngine;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>
    /// Self-describing component on preview prefabs (ghost and confirmed variants).
    /// Exposes direct references to its own renderers -- no hierarchy searching.
    /// Both the ghost preview (during drag) and confirmed placeholder prefabs carry this.
    ///
    /// Turns with the camera: the sticker is authored reading upright from
    /// side 0, so the piece takes the camera's yaw, and follows it if the POV
    /// changes while the piece is on the board. Each piece is one SortingGroup
    /// -- ordered against its neighbours by the camera's sort axis -- with the
    /// sticker one height above the cube, so the sticker is always drawn on top
    /// of it, including through the ghost's transparent cube.
    /// </summary>
    public class DraftPlacementPreview : MonoBehaviour
    {
        // The sticker sits on the cube; only its height above it matters.
        private const int StickerHeight = 1;

        [Header("Renderers")]
        [SerializeField] private MeshRenderer baseMeshRenderer;
        [SerializeField] private SpriteRenderer stickerRenderer;

        private Camera viewCamera;
        private SpriteDepthSorter sorter;
        private int appliedSide = -1;

        private void Awake()
        {
            viewCamera = Camera.main;
            if (baseMeshRenderer == null || stickerRenderer == null) return;

            // The group competes on the layer and order the cube had; inside it
            // the sorter owns the order, so the renderers are then re-ordered.
            SortHeight.Ensure(baseMeshRenderer, 0);
            SortHeight.Ensure(stickerRenderer, StickerHeight);
            stickerRenderer.sortingLayerID = baseMeshRenderer.sortingLayerID;
            sorter = SpriteDepthSorter.Attach(gameObject,
                baseMeshRenderer.sortingLayerID, baseMeshRenderer.sortingOrder);

            FollowCamera();
        }

        private void LateUpdate()
        {
            FollowCamera();
        }

        // A compare of two ints per frame; the yaw and the re-order only happen
        // when the camera has actually changed side.
        private void FollowCamera()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            int side = ViewSide.FromYaw(viewCamera.transform.eulerAngles.y);
            if (side == appliedSide) return;

            appliedSide = side;
            transform.rotation = Quaternion.Euler(0f, ViewSide.Yaw(side), 0f);
            if (sorter != null) sorter.Apply(side);
        }

        public void SetSticker(Sprite sprite)
        {
            if (stickerRenderer == null) return;
            stickerRenderer.sprite = sprite;
            // Don't disable renderer -- keep it enabled so base tint remains visible.
            // Null sprite simply means no icon overlay; the base quad still shows.
        }

        public void SetAlpha(float alpha)
        {
            if (baseMeshRenderer != null)
            {
                Color c = baseMeshRenderer.material.color;
                c.a = alpha;
                baseMeshRenderer.material.color = c;
            }

            if (stickerRenderer != null)
            {
                Color sc = stickerRenderer.color;
                sc.a = alpha;
                stickerRenderer.color = sc;
            }
        }

        public void SetTint(Color tint)
        {
            if (baseMeshRenderer == null) return;
            // Preserve current alpha (set by zone logic), only change RGB
            float currentAlpha = baseMeshRenderer.material.color.a;
            tint.a = currentAlpha;
            baseMeshRenderer.material.color = tint;
        }

        public void SetTintWithAlpha(Color tint)
        {
            if (baseMeshRenderer != null)
                baseMeshRenderer.material.color = tint;
        }
    }
}