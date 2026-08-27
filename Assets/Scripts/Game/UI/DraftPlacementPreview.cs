using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Self-describing component on preview prefabs (ghost and confirmed variants).
    /// Exposes direct references to its own renderers — no hierarchy searching.
    /// Both the ghost preview (during drag) and confirmed placeholder prefabs carry this.
    /// </summary>
    public class DraftPlacementPreview : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] private MeshRenderer baseMeshRenderer;
        [SerializeField] private SpriteRenderer stickerRenderer;

        public void SetSticker(Sprite sprite)
        {
            if (stickerRenderer == null) return;
            stickerRenderer.sprite = sprite;
            // Don't disable renderer — keep it enabled so base tint remains visible.
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