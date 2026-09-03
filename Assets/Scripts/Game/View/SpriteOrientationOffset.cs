using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>
    /// Per-sprite rotation offset. Attached to individual sprite GameObjects
    /// that need to sit differently from the standard NodePresentation rotation.
    /// NodePresentation checks for this component during orientation application.
    /// </summary>
    public class SpriteOrientationOffset : MonoBehaviour
    {
        [Tooltip("Additive euler offset applied to this sprite only.")]
        public Vector3 offset;

        private void Start()
        {
            GetComponent<SpriteRenderer>().shadowCastingMode = ShadowCastingMode.On;
        }
    }
}