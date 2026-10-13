using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Drives the white touch-down flash on a villager.
    ///
    /// The flash fires the instant a finger goes down, before the gesture has
    /// resolved into a tap, a pan or a long press. That is the point of it:
    /// acknowledgement cannot wait for the outcome, or the game feels
    /// unresponsive for the length of the slop and long-press windows.
    ///
    /// If the press turns out to be a pan, nothing needs undoing -- the flash
    /// decays on its own and reads as "I saw you touch that", not as a
    /// selection that was then taken away.
    ///
    /// Only the amount is held here; VillagerView owns the colour, so the flash
    /// composes with the per-state tint instead of fighting it.
    /// </summary>
    public class VillagerFlash : MonoBehaviour
    {
        private VillagerView view;
        private float duration = 0.12f;
        private float elapsed;
        private bool active;

        public void Initialize(VillagerView villagerView, float flashDuration)
        {
            view = villagerView;
            duration = Mathf.Max(0.01f, flashDuration);
        }

        /// <summary>
        /// Restarts the envelope at full white. Touch, order and damage all call
        /// this, so overlapping sources share one restart rather than stacking.
        /// </summary>
        public void Flash()
        {
            elapsed = 0f;
            active = true;
            Apply(1f);
        }

        /// <summary>Drops any flash in progress, e.g. when the villager dies.</summary>
        public void Clear()
        {
            if (!active) return;
            active = false;
            elapsed = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (!active) return;

            // Unscaled: input feedback must not stall with the simulation or a
            // transition animation.
            elapsed += Time.unscaledDeltaTime;
            float amount = VillagerFlashMath.Amount(elapsed, duration);

            if (amount <= 0f) active = false;
            Apply(amount);
        }

        private void Apply(float amount)
        {
            if (view != null) view.SetFlashAmount(amount);
        }
    }
}
