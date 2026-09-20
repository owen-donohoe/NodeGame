using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// How far up a renderer sits in its SortingGroup, as an authored integer.
    /// Depth is automatic (SpriteDepthSorter orders by distance along the POV
    /// sort axis); height is not, because nothing in the transforms says that a
    /// piece of food sits on a stand. Higher always draws in front of lower,
    /// whatever their depth. Equal heights order by depth. No component means 0.
    /// </summary>
    public class SortHeight : MonoBehaviour
    {
        [SerializeField] private int height;

        public int Height => height;

        /// <summary>
        /// For prefabs that cannot be edited from code: adds the component with
        /// a height unless one is already authored.
        /// </summary>
        public static void Ensure(Component target, int defaultHeight)
        {
            if (target == null || target.GetComponent<SortHeight>() != null) return;
            target.gameObject.AddComponent<SortHeight>().height = defaultHeight;
        }
    }
}
