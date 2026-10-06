using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Applies a district's board tuning (DistrictVisual.boardOffset / boardEuler / boardScale) to a
    /// freshly spawned node.
    ///
    /// WHERE IT GOES. A node prefab's root is the ground tile and the node itself: it carries NodeView,
    /// the colliders the press resolves against, and the position villagers and routes are placed from.
    /// None of that should move when someone nudges a building, so the root is left alone. The art is
    /// the root's children (the building sprites or a 3D model), and the tuning is applied to those, as
    /// a group, by moving them under one pivot at the node's centre:
    ///
    ///   root (ground, NodeView, colliders)        stays at the node
    ///   +- BoardArt (pivot: offset, euler, scale)    the tuning
    ///      +- building sprites / 3D model          keep their prefab-local transforms
    ///
    /// So the tuning composes on top of the prefab's own layout rather than replacing it. A child that
    /// holds a Canvas or a Collider (the claim bar, a hit volume) is not art and stays on the root.
    /// With the default tuning nothing is touched at all, so no node changes until someone sets a value.
    /// A model that sits directly on the root (a MeshRenderer on the root itself) cannot be nudged this
    /// way; put it under a child in the prefab.
    /// </summary>
    public static class BoardArtPlacer
    {
        public const string PivotName = "BoardArt";

        private static readonly List<Transform> artChildren = new List<Transform>();

        public static void Apply(GameObject instance, DistrictVisual visual)
        {
            if (instance == null || visual == null || !visual.HasBoardTuning) return;

            Transform root = instance.transform;

            artChildren.Clear();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (IsArt(child)) artChildren.Add(child);
            }
            if (artChildren.Count == 0) return;

            Transform pivot = new GameObject(PivotName).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = visual.boardOffset;
            pivot.localRotation = Quaternion.Euler(visual.boardEuler);
            pivot.localScale = Vector3.one * BoardArtRules.SafeScale(visual.boardScale);

            // false keeps each child's local transform, which is the prefab's layout.
            for (int i = 0; i < artChildren.Count; i++) artChildren[i].SetParent(pivot, false);
            artChildren.Clear();
        }

        private static bool IsArt(Transform child)
        {
            if (child.GetComponentInChildren<Canvas>(true) != null) return false;
            if (child.GetComponentInChildren<Collider>(true) != null) return false;
            return child.GetComponentInChildren<Renderer>(true) != null;
        }
    }
}
