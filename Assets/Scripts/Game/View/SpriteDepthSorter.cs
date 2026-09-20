using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>
    /// Orders the renderers inside one SortingGroup: sortingOrder becomes
    /// height * OrderStride + depth rank (ViewSide.ComputeOrders). The group
    /// itself is ordered against other objects by the camera's sort axis, so a
    /// node or a draft piece is drawn as one unit, in front of or behind its
    /// neighbours by depth.
    ///
    /// Nothing runs per frame. Apply is called when the POV changes and Refresh
    /// when renderers are added, and both reuse the arrays sized in Refresh.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public class SpriteDepthSorter : MonoBehaviour
    {
        private Renderer[] renderers = new Renderer[0];
        private int[] heights = new int[0];
        private float[] depths = new float[0];
        private int[] orders = new int[0];
        private int side;

        /// <summary>
        /// Makes root a sorting group on the given layer and order, with a sorter.
        /// The layer is what the group as a whole competes on, so it is the one
        /// thing a caller has to choose.
        /// </summary>
        public static SpriteDepthSorter Attach(GameObject root, int sortingLayerID, int sortingOrder)
        {
            SortingGroup group = root.GetComponent<SortingGroup>();
            if (group == null) group = root.AddComponent<SortingGroup>();
            group.sortingLayerID = sortingLayerID;
            group.sortingOrder = sortingOrder;

            SpriteDepthSorter sorter = root.GetComponent<SpriteDepthSorter>();
            if (sorter == null) sorter = root.AddComponent<SpriteDepthSorter>();
            sorter.Refresh();
            return sorter;
        }

        /// <summary>
        /// Re-reads the sprite and mesh renderers under this group and their
        /// heights, then re-applies the current side. Call after adding one.
        /// </summary>
        public void Refresh()
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);

            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is SpriteRenderer || found[i] is MeshRenderer) count++;
            }

            renderers = new Renderer[count];
            heights = new int[count];
            depths = new float[count];
            orders = new int[count];

            int n = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (!(found[i] is SpriteRenderer || found[i] is MeshRenderer)) continue;

                renderers[n] = found[i];
                SortHeight authored = found[i].GetComponent<SortHeight>();
                heights[n] = authored != null ? authored.Height : 0;
                n++;
            }

            Apply(side);
        }

        /// <summary>Re-orders for a viewing side. Cheap; only needed when the side changes.</summary>
        public void Apply(int viewSide)
        {
            side = ViewSide.Wrap(viewSide);

            // A renderer destroyed since Refresh keeps its slot with a depth of 0;
            // ordering the rest around it is harmless and it is never written.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Vector3 p = renderers[i].transform.position;
                depths[i] = ViewSide.Depth(side, p.x, p.z);
            }

            ViewSide.ComputeOrders(depths, heights, renderers.Length, orders);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].sortingOrder = orders[i];
            }
        }
    }
}
