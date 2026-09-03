using UnityEngine;
using NodeWar.View;

namespace NodeWar.Input
{
    /// <summary>
    /// The one bridge from gesture events to renderers.
    ///
    /// Input publishes what happened; the View decides what that looks like.
    /// Without a seam like this, the gesture source ends up reaching into
    /// SpriteRenderers directly and the input layer starts owning appearance.
    ///
    /// Fires on touch-down -- before the gesture resolves -- and again on tap.
    /// Touch-down feedback cannot wait for the outcome, or the game feels dead
    /// for the length of the slop and long-press windows. A press that becomes
    /// a pan simply lets the flash decay, which reads as "I saw that" rather
    /// than as a selection being withdrawn.
    /// </summary>
    public class HitFlashRouter : MonoBehaviour
    {
        private PointerGestureSource source;
        private Transform[] villagerTransforms;

        public void Initialize(PointerGestureSource gestureSource)
        {
            Unsubscribe();
            source = gestureSource;
            Subscribe();
        }

        /// <summary>
        /// Shares GameManager's existing villager transform array rather than
        /// keeping a parallel one. The array is rebuilt whenever villagers
        /// spawn, and a second copy would be one more thing to forget to
        /// resync.
        /// </summary>
        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
        }

        private void Subscribe()
        {
            if (source == null) return;
            source.OnPointerDown += HandleFlashTarget;
            source.OnTap += HandleFlashTarget;
        }

        private void Unsubscribe()
        {
            if (source == null) return;
            source.OnPointerDown -= HandleFlashTarget;
            source.OnTap -= HandleFlashTarget;
        }

        private void OnDestroy() => Unsubscribe();

        private void HandleFlashTarget(GestureTarget target)
        {
            if (target.kind != GestureTargetKind.Villager) return;
            if (villagerTransforms == null) return;
            if (target.id < 0 || target.id >= villagerTransforms.Length) return;

            Transform t = villagerTransforms[target.id];
            if (t == null) return;

            // Looked up per press rather than cached: this runs once per touch,
            // not per frame, and it cannot go stale when villagers respawn.
            VillagerFlash flash = t.GetComponent<VillagerFlash>();
            if (flash != null) flash.Flash();
        }
    }
}
