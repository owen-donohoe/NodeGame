using UnityEngine;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>Storehouse bank contents, outside the district art's SortingGroup.</summary>
    public sealed class BankPips : MonoBehaviour
    {
        private SimulationState state;
        private int nodeID;
        private float nodeScale;
        private Camera viewCamera;
        private Transform root;
        private SpriteRenderer[] pips;
        private static Sprite[] sprites;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSprites() { sprites = null; }

        private static Sprite ResourceSprite(int resource)
        {
            if (sprites == null) sprites = new Sprite[3];
            if (sprites[resource] != null) return sprites[resource];
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (StructurePresentation.ShapePixel(resource + 1, x, y))
                        pixels[y * 16 + x] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprites[resource] = sprite;
        }

        public void Initialize(SimulationState simulation, int id, float scale)
        {
            state = simulation;
            nodeID = id;
            nodeScale = scale;
            viewCamera = Camera.main;
            root = new GameObject("BankPips").transform;
            root.SetParent(transform, false);
            Vector3 parentScale = transform.lossyScale;
            root.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);
            pips = new SpriteRenderer[StructurePresentation.MaxPips];
            for (int i = 0; i < pips.Length; i++)
            {
                var go = new GameObject("Bank" + i);
                go.transform.SetParent(root, false);
                var pip = go.AddComponent<SpriteRenderer>();
                pip.sortingLayerID = SortingLayer.NameToID("Villagers");
                pip.sortingOrder = StructurePresentation.FillOrder;
                pip.transform.localScale = Vector3.one * (StructurePresentation.PipSize * nodeScale);
                pips[i] = pip;
            }
            Update();
        }

        private void Update()
        {
            if (root == null || state == null) return;
            NodeData node = state.nodes[nodeID];
            int count = StructurePresentation.PipCount(node);
            root.gameObject.SetActive(count > 0);
            for (int i = 0; i < pips.Length; i++)
            {
                int resource = StructurePresentation.PipResource(node, i);
                pips[i].enabled = resource >= 0;
                if (resource < 0) continue;
                pips[i].sprite = ResourceSprite(resource);
                StructurePresentation.ResourceTint(resource, out float r, out float g, out float b);
                pips[i].color = new Color(r, g, b);
                pips[i].transform.localPosition = new Vector3(
                    StructurePresentation.PipX(i, count) * nodeScale, StructurePresentation.PipY * nodeScale, 0f);
            }
        }

        private void LateUpdate()
        {
            if (root == null) return;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            root.rotation = viewCamera.transform.rotation;
            int side = ViewSide.FromYaw(viewCamera.transform.eulerAngles.y);
            float horizontal = new Vector2(viewCamera.transform.up.x, viewCamera.transform.up.z).magnitude;
            StructurePresentation.GroundOffset(side, viewCamera.transform.up.y, horizontal,
                out float x, out float y, out float z);
            root.position = transform.position + new Vector3(x, y, z) * nodeScale;
        }
    }
}
