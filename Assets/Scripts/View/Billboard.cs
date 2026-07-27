using UnityEngine;

namespace NodeWar.View
{
    public class Billboard : MonoBehaviour
    {
        // Fixed facing direction (not "look at camera" — a consistent world angle)
        // This gets set once and stays unless overridden
        private static Quaternion sharedFacing;
        private static bool facingInitialized = false;

        [Tooltip("Override the shared facing for this specific object")]
        public bool useCustomAngle = false;
        public Vector3 customEulerAngles;

        private void Start()
        {
            if (!facingInitialized)
            {
                // Default: face toward camera's forward direction, but fixed
                // For isometric with camera at (45, 0, 0) rotation, sprites
                // should face roughly (45, 0, 0) to appear upright to the viewer
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Match camera's X rotation so sprites appear "standing up"
                    // but lock Y/Z so they don't track the camera
                    sharedFacing = Quaternion.Euler(cam.transform.eulerAngles.x, 0f, 0f);
                }
                else
                {
                    sharedFacing = Quaternion.Euler(50f, 0f, 0f);
                }
                facingInitialized = true;
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

        // Call this if camera angle changes mid-game (unlikely but safe)
        // might have opposite of intent, most times as cam pans you want sprites to NOT billboard with it
        // could be used for cutscenes down the line
        public static void RecalculateFacing(float xAngle)
        {
            sharedFacing = Quaternion.Euler(xAngle, 0f, 0f);
            facingInitialized = true;
        }
    }
}