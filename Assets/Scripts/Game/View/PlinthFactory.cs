using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>Board-owned shared resources, with a prefab-independent mesh fallback.</summary>
    public sealed class PlinthFactory : System.IDisposable
    {
        private static readonly System.Collections.Generic.List<PlinthFactory> active = new System.Collections.Generic.List<PlinthFactory>();
        private static Material fallbackMaterial;
        public static void ApplyTransparency(bool enabled)
        {
            foreach (PlinthFactory factory in active)
                factory.SetUnderwaterFade(enabled);
        }
        private static int fallbackUsers;
        private readonly System.Collections.Generic.List<Renderer> renderers = new System.Collections.Generic.List<Renderer>();
        private readonly MaterialPropertyBlock fadeProperties = new MaterialPropertyBlock();
        private void SetUnderwaterFade(bool enabled)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(fadeProperties);
                fadeProperties.SetFloat("_UnderwaterFade", enabled ? 0.15f : 0.3f);
                renderer.SetPropertyBlock(fadeProperties);
            }
        }
        private bool disposed;
        private readonly GameObject prefab;
        private readonly Material material;
        private Mesh fallback;
        private readonly bool ownsMaterial;
        private readonly float waterY;

        public PlinthFactory(Color topColor, float waterY)
        {
            prefab = Resources.Load<GameObject>("Board/DistrictPlinth");
            material = Resources.Load<Material>("Board/DistrictPlinth");
            ownsMaterial = material == null;
            if (ownsMaterial)
            {
                if (fallbackMaterial == null)
                    fallbackMaterial = new Material(Resources.Load<Shader>("Board/NodeWarPlinth")) { name = "District plinth fallback" };
                material = fallbackMaterial;
                fallbackUsers++;
            }
            this.waterY = waterY;
            active.Add(this);
            ApplyTransparency(WaterQuality.Has(GraphicsFeature.Transparency));
        }

        public GameObject Create(Transform parent, string name, float cellScale)
        {
            GameObject root;
            if (prefab != null) root = Object.Instantiate(prefab, parent, false);
            else
            {
                root = new GameObject(name);
                root.transform.SetParent(parent, false);
                if (fallback == null) fallback = BuildCube();
                root.AddComponent<MeshFilter>().sharedMesh = fallback;
                root.AddComponent<MeshRenderer>();
            }
            root.name = name;
            root.transform.localScale = Vector3.one * cellScale;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Cover both imported material slots without making per-cell materials.
                int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                var slots = new Material[count];
                for (int i = 0; i < count; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_WaterY", waterY);
                properties.SetFloat("_UnderwaterFade", WaterQuality.Has(GraphicsFeature.Transparency) ? 0.15f : 0.3f);
                renderers.Add(renderer);
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = false;
            }
            return root;
        }

        private static Mesh BuildCube()
        {
            // Separate face vertices preserve hard normals and independent UVs.
            var vertices = new Vector3[24];
            var colors = new Color32[24];
            var uv = new Vector2[24];
            var indices = new int[36];
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            Vector3[] horizontal = { Vector3.right, Vector3.right, Vector3.left, Vector3.back, Vector3.right, Vector3.forward };
            for (int face = 0; face < 6; face++)
            {
                Vector3 n = normals[face], u = horizontal[face];
                Vector3 v = Vector3.Cross(n, u);
                Vector3 centre = new Vector3(0f, -PlinthLayout.Height * 0.5f, 0f);
                centre += n * (face < 2 ? PlinthLayout.Height * 0.5f : 0.5f);
                float halfV = face < 2 ? 0.5f : PlinthLayout.Height * 0.5f;
                int first = face * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    float a = corner == 0 || corner == 3 ? 0f : 1f;
                    float b = corner < 2 ? 0f : 1f;
                    vertices[first + corner] = centre + u * (a - 0.5f) + v * ((b * 2f - 1f) * halfV);
                    uv[first + corner] = face < 2 ? new Vector2(a, b) : new Vector2((face - 2 + a) / 4f, b);
                    colors[first + corner] = new Color32(255, 255, 255, 255);
                }
                int t = face * 6;
                indices[t] = first; indices[t + 1] = first + 1; indices[t + 2] = first + 2;
                indices[t + 3] = first; indices[t + 4] = first + 2; indices[t + 5] = first + 3;
            }
            var mesh = new Mesh { name = "Plinth fallback", vertices = vertices, triangles = indices, uv = uv, colors32 = colors };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            active.Remove(this);
            Release(fallback);
            if (ownsMaterial && --fallbackUsers == 0)
            {
                Release(fallbackMaterial);
                fallbackMaterial = null;
            }
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
