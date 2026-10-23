using NodeWar.Lobby;
using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>Owns the runtime water resources; terrain is copied, never modified.</summary>
    public sealed class WaterSurfaceView : MonoBehaviour
    {
        private const int Padding = 4;
        private int cols, rows, subdivisions;
        private float scale, surfaceY, blend = 1f;
        private TerrainType[] terrain;
        private Material material;
        private Mesh mesh;
        private MeshFilter filter;
        private Texture2D current, previous;

        private static bool ReducedMotion => PlayerProfile.Instance != null && PlayerProfile.Instance.Settings.reducedMotion;

        public void Initialize(int columns, int rowCount, float cellScale, float y)
        {
            cols = columns; rows = rowCount; scale = cellScale; surfaceY = y;
            int tier = WaterQuality.Current;
            subdivisions = WaterMeshBuilder.SubdivisionsForTier(tier);
            material = WaterMaterialFactory.Create(tier);
            filter = gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        public void SetTerrain(TerrainType[] resolved)
        {
            if (SameTerrain(resolved)) return;
            ShoreFieldData field = ShoreField.Bake(cols, rows, resolved);
            var texture = new Texture2D(field.width, field.height, TextureFormat.RGBA32, false, true)
            {
                name = "Water shore field", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.LoadRawTextureData(field.rgba);
            texture.Apply(false, false);

            // Preserve the currently visible field when another edit interrupts a blend.
            if (previous != null && previous != current)
            {
                if (blend < 1f)
                {
                    Color32[] from = previous.GetPixels32(), to = current.GetPixels32();
                    for (int i = 0; i < to.Length; i++) to[i] = Color32.Lerp(from[i], to[i], blend);
                    current.SetPixels32(to);
                    current.Apply(false, false);
                }
                Release(previous);
            }
            previous = current != null ? current : texture;
            current = texture;
            blend = previous == current || ReducedMotion ? 1f : 0f;
            material.SetTexture("_ShoreTex", current);
            material.SetTexture("_ShorePrevTex", previous);
            material.SetFloat("_ShoreBlend", blend);
            Vector3 origin = transform.TransformPoint(new Vector3(field.originX * scale, surfaceY, field.originZ * scale));
            material.SetVector("_ShoreST", new Vector4(origin.x, origin.z,
                1f / (field.width * field.worldPerTexel * scale), 1f / (field.height * field.worldPerTexel * scale)));

            var data = WaterMeshBuilder.Build(cols, rows, resolved, subdivisions, Padding);
            var vertices = new Vector3[data.positions.Length / 3];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(data.positions[i * 3] * scale, surfaceY, data.positions[i * 3 + 2] * scale);
            var nextMesh = new Mesh { name = "Water surface", indexFormat = IndexFormat.UInt32 };
            nextMesh.vertices = vertices;
            nextMesh.triangles = data.indices;
            nextMesh.RecalculateBounds();
            // Gerstner displacement happens on the GPU, so CPU culling needs its envelope.
            Bounds bounds = nextMesh.bounds;
            bounds.Expand(2f);
            nextMesh.bounds = bounds;
            filter.sharedMesh = nextMesh;
            Release(mesh);
            mesh = nextMesh;
            terrain = (TerrainType[])resolved.Clone();
            if (blend == 1f) FinishBlend();
        }

        private bool SameTerrain(TerrainType[] resolved)
        {
            if (terrain == null || resolved == null || terrain.Length != resolved.Length) return false;
            for (int i = 0; i < terrain.Length; i++) if (terrain[i] != resolved[i]) return false;
            return true;
        }

        private void Update()
        {
            if (blend >= 1f) return;
            blend = ReducedMotion ? 1f : Mathf.Min(1f, blend + Time.unscaledDeltaTime / 0.4f);
            material.SetFloat("_ShoreBlend", blend);
            if (blend == 1f) FinishBlend();
        }

        private void FinishBlend()
        {
            material.SetTexture("_ShorePrevTex", current);
            if (previous != current) Release(previous);
            previous = current;
        }

        private void OnDestroy()
        {
            if (previous != current) Release(previous);
            Release(current); Release(mesh); Release(material);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
