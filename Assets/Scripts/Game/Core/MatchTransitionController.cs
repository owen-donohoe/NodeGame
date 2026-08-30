using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Simulation;
using NodeWar.Config;
using NodeWar.UI;
using NodeWar.View;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace NodeWar.Core
{
    /// <summary>
    /// Orchestrates visual transitions between match phases:
    ///   - Post-draft startup: placeholders fly away --> nodes spring up --> countdown --> play
    ///   - Game over: nodes collapse outward from losing core
    /// 
    /// All timing and animation parameters are Inspector-configurable.
    /// Stateless between sequences — receives data per invocation via method parameters.
    /// Lives on the same GameObject as GameManager or a sibling; assigned via Inspector.
    /// </summary>
    public class MatchTransitionController : MonoBehaviour
    {
        [Header("Placeholder Removal")]
        [Tooltip("How far up placeholders fly before being destroyed.")]
        [SerializeField] private float placeholderFlyUpDistance = 10f;
        [Tooltip("Duration of each placeholder's fly-up animation.")]
        [SerializeField] private float placeholderFlyDuration = 0.8f;
        [Tooltip("Delay between each successive placeholder starting its fly-up.")]
        [SerializeField] private float placeholderStaggerDelay = 0.12f;
        [SerializeField] private Ease placeholderFlyEase = Ease.InBack;

        [Header("Post-Removal Timing")]
        [Tooltip("Seconds to wait after all placeholders begin removal before starting node startup.")]
        [SerializeField] private float delayAfterPlaceholderRemoval = 1.5f;
        [Tooltip("Seconds to wait after node startup wave begins before showing countdown.")]
        [SerializeField] private float delayAfterNodeStartup = 1.0f;

        [Header("Node Startup Wave")]
        [Tooltip("Delay per manhattan-distance unit from grid center. Controls wave speed.")]
        [SerializeField] private float nodeStartupDelayPerGridDistance = 0.14f;

        [Header("Node Breakdown Wave (Game Over)")]
        [Tooltip("Delay per world-unit distance from the losing core. Controls collapse wave speed.")]
        [SerializeField] private float nodeBreakdownDelayPerWorldUnit = 0.04f;

        [Header("Countdown")]
        [Tooltip("Prefab with CountdownUI component. Screen-space overlay canvas.")]
        [SerializeField] private GameObject countdownPrefab;

        // Events
        /// <summary>
        /// Fired at the moment the transition needs the camera/sprites to switch to gameplay side.
        /// GameManager should call SetPlayerSide in response.
        /// Passes the localPlayerID that was provided to PlayPostDraftTransition.
        /// </summary>
        public System.Action<int> OnRequestPlayerSideSwitch;

        /// <summary>
        /// Fired when the full startup transition is complete (countdown finished or skipped).
        /// GameManager should unpause tick providers in response.
        /// </summary>
        public System.Action OnStartupTransitionComplete;

        // ===== PUBLIC API =====

        /// <summary>
        /// Runs the full post-draft startup sequence:
        ///   1. Camera exits draft mode, player side switches
        ///   2. Placeholders animate away
        ///   3. Wait for removal
        ///   4. Nodes spring up in wave from center
        ///   5. Wait for wave to settle
        ///   6. Countdown plays (3-2-1-GO)
        ///   7. OnStartupTransitionComplete fires
        /// </summary>
        public void PlayPostDraftTransition(int localPlayerID,
            List<GameObject> placeholdersToRemove,
            NodePresentation[] nodePresentations,
            SimulationState state, BoardConfig boardConfig)
        {
            StartCoroutine(PostDraftSequence(
                localPlayerID, placeholdersToRemove, nodePresentations, state, boardConfig));
        }

        /// <summary>
        /// Plays only the node startup wave animation. No countdown, no delays, no events.
        /// Used by testing mode where gameplay starts immediately.
        /// </summary>
        public void PlayNodeStartupWave(NodePresentation[] nodePresentations,
            SimulationState state, BoardConfig boardConfig)
        {
            if (nodePresentations == null) return;

            float centerX = (boardConfig.Data.gridCols - 1) * 0.5f;
            float centerZ = (boardConfig.Data.gridRows - 1) * 0.5f;

            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] == null) continue;

                NodeData node = state.nodes[i];
                float dist = Mathf.Abs(node.gridX - centerX) + Mathf.Abs(node.gridZ - centerZ);
                float delay = dist * nodeStartupDelayPerGridDistance;

                nodePresentations[i].SetHidden();
                nodePresentations[i].PlayStartup(delay);
            }
        }

        /// <summary>
        /// Plays node breakdown wave originating from the losing player's core.
        /// Used on game over.
        /// </summary>
        public void PlayNodeBreakdownWave(NodePresentation[] nodePresentations,
            SimulationState state)
        {
            if (nodePresentations == null) return;

            int loserID = state.winnerID == 0 ? 1 : 0;
            int originNode = state.players[loserID].coreNodeID;
            Vector3 origin = nodePresentations[originNode] != null
                ? nodePresentations[originNode].transform.position
                : Vector3.zero;

            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] == null) continue;

                float dist = Vector3.Distance(nodePresentations[i].transform.position, origin);
                float delay = dist * nodeBreakdownDelayPerWorldUnit;

                nodePresentations[i].PlayBreakdown(delay);
            }
        }

        // ===== SEQUENCES =====

        private IEnumerator PostDraftSequence(int localPlayerID,
            List<GameObject> placeholders,
            NodePresentation[] nodePresentations,
            SimulationState state, BoardConfig boardConfig)
        {
            // Step 1: Switch camera to gameplay framing
            OnRequestPlayerSideSwitch?.Invoke(localPlayerID);

            // Step 2: Animate placeholders away
            AnimatePlaceholdersAway(placeholders);

            // Step 3: Wait for removal to finish visually
            yield return new WaitForSeconds(delayAfterPlaceholderRemoval);

            // Step 4: Nodes spring up in wave
            PlayNodeStartupWave(nodePresentations, state, boardConfig);

            // Step 5: Wait for wave to settle before countdown
            yield return new WaitForSeconds(delayAfterNodeStartup);

            // Step 6: Countdown (or skip if no prefab)
            if (countdownPrefab != null)
            {
                GameObject countdownGO = Instantiate(countdownPrefab);
                CountdownUI countdown = countdownGO.GetComponent<CountdownUI>();
                if (countdown != null)
                {
                    bool countdownFinished = false;
                    countdown.OnCountdownComplete += () => countdownFinished = true;
                    countdown.StartCountdown();

                    while (!countdownFinished)
                        yield return null;
                }
            }

            // Step 7: Signal completion
            OnStartupTransitionComplete?.Invoke();
        }

        private void AnimatePlaceholdersAway(List<GameObject> placeholders)
        {
            if (placeholders == null || placeholders.Count == 0) return;

            for (int i = 0; i < placeholders.Count; i++)
            {
                if (placeholders[i] == null) continue;

                GameObject obj = placeholders[i];
                float delay = i * placeholderStaggerDelay;

                obj.transform.DOMove(
                        obj.transform.position + Vector3.up * placeholderFlyUpDistance,
                        placeholderFlyDuration)
                    .SetDelay(delay)
                    .SetEase(placeholderFlyEase)
                    .OnComplete(() => Destroy(obj));
            }
        }
    }
}