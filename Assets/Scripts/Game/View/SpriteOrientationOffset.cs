using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>
    /// Per-sprite rotation offset. Attached to individual sprite GameObjects
    /// that need to sit differently from the standard NodePresentation rotation.
    /// NodePresentation and Billboard compose this once on their base facing.
    /// </summary>
    public class SpriteOrientationOffset : MonoBehaviour
    {
        [Tooltip("Local euler rotation composed once on top of this sprite's base facing.")]
        public Vector3 offset;

        public Quaternion Compose(Quaternion baseFacing)
            => baseFacing * Quaternion.Euler(offset);

        private void Start()
        {
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
