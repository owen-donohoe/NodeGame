using UnityEngine;

namespace NodeWar.View
{
    public class Billboard : MonoBehaviour
    {
        // Fixed facing direction (not "look at camera" � a consistent world angle)
        // Capture the camera facing once per instance.
        private Quaternion facing;

        [Tooltip("Override the camera facing for this object")]
        public bool useCustomAngle = false;
        public Vector3 customEulerAngles;

        private void Start()
        {
            // Default: face toward camera's forward direction, but fixed
            // For isometric with camera at (45, 0, 0) rotation, sprites
            // should face roughly (45, 0, 0) to appear upright to the viewer
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Keep the initial camera rotation as the camera moves.
                facing = cam.transform.rotation;
            }
            else
            {
                facing = Quaternion.Euler(50f, 0f, 0f);
            }

            ApplyFacing();
        }

        private void LateUpdate()
        {
            // LateUpdate so it runs after any parent position changes
            if (!useCustomAngle)
            {
                transform.rotation = facing;
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
                transform.rotation = facing;
            }
        }
    }
}