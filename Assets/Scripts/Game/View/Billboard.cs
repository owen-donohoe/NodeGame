using UnityEngine;
using UnityEngine.SceneManagement;

namespace NodeWar.View
{
    public class Billboard : MonoBehaviour
    {
        // Fixed facing direction (not "look at camera" -- a consistent world angle).
        // One facing is shared by every Billboard so sprites created at different
        // moments (draft, match start, mid-match spawns) never disagree. The first
        // Billboard to Start in a scene captures it; a later scene load (the next
        // match) recaptures rather than inheriting the previous scene's value.
        private static Quaternion sharedFacing;
        private static SceneHandle facingSceneHandle;
        private static bool facingInitialized;

        [Tooltip("Override the shared facing for this specific object")]
        public bool useCustomAngle = false;
        public Vector3 customEulerAngles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            facingInitialized = false;
            facingSceneHandle = default;
        }

        private void Start()
        {
            SceneHandle sceneHandle = gameObject.scene.handle;
            if (!facingInitialized || facingSceneHandle != sceneHandle)
            {
                // Default: face toward camera's forward direction, but fixed
                // For isometric with camera at (45, 0, 0) rotation, sprites
                // should face roughly (45, 0, 0) to appear upright to the viewer
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Keep the camera rotation seen at capture as the camera moves.
                    sharedFacing = cam.transform.rotation;
                }
                else
                {
                    sharedFacing = Quaternion.Euler(50f, 0f, 0f);
                }
                facingInitialized = true;
                facingSceneHandle = sceneHandle;
            }

            ApplyFacing();
        }

        private void LateUpdate()
        {
            // LateUpdate so it runs after any parent position changes
            if (!useCustomAngle)
            {
                transform.rotation = sharedFacing;
            }
        }

        private void ApplyFacing()
        {
            if (useCustomAngle)
            {
                transform.rotation = Quaternion.Euler(customEulerAngles);
            }
            else
            {
                transform.rotation = sharedFacing;
            }
        }
    }
}