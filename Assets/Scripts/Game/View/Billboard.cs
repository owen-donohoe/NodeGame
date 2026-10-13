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

        [Tooltip("Use a fixed pitch from vertical for sprite art. Canvas billboards remain camera-facing.")]
        public bool useUprightPitch = true;
        [Range(0f, 89f)] public float uprightPitch = VillagerTiltMath.DefaultPitch;

        private bool hasCanvas;
        private SpriteOrientationOffset orientationOffset;

        private void Awake()
        {
            hasCanvas = GetComponent<Canvas>() != null;
            orientationOffset = GetComponent<SpriteOrientationOffset>();
        }

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
                Capture(sceneHandle, Camera.main);

            ApplyFacing();
        }

        /// <summary>
        /// Captures the shared facing from a camera now. The first capture in a
        /// scene happens at whatever the camera shows when the first Billboard
        /// starts (the draft's pitch, say), so anything that turns the camera
        /// afterwards -- a side change, the draft giving its pitch back -- must
        /// call this. Billboards re-apply the shared facing every LateUpdate.
        /// CameraController.SetPOV is the caller.
        /// </summary>
        public static void RecaptureFacing(Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            Capture(cam != null ? cam.gameObject.scene.handle : SceneManager.GetActiveScene().handle, cam);
        }

        private static void Capture(SceneHandle sceneHandle, Camera cam)
        {
            // Default: face toward camera's forward direction, but fixed
            // For isometric with camera at (45, 0, 0) rotation, sprites
            // should face roughly (45, 0, 0) to appear upright to the viewer
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

        private void LateUpdate()
        {
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (useCustomAngle)
            {
                transform.rotation = Quaternion.Euler(customEulerAngles);
            }
            else
            {
                // UI is independent of the sprite pitch, even on older prefabs.
                bool upright = useUprightPitch && !hasCanvas;
                Vector3 cameraEuler = sharedFacing.eulerAngles;
                var facing = VillagerTiltMath.Facing(cameraEuler.x, cameraEuler.y,
                    cameraEuler.z, upright, uprightPitch);
                Quaternion rotation = upright
                    ? Quaternion.Euler(facing.Pitch, facing.Yaw, facing.Roll)
                    : sharedFacing;
                transform.rotation = orientationOffset != null ? orientationOffset.Compose(rotation) : rotation;
            }
        }
    }
}
