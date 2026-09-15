using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The scenery behind the lobby: a warm sky and a ground band.
    ///
    /// It stands in for a 3D lobby scene that does not exist, exactly as the
    /// prototype's CSS does. USS has no gradients, so both are drawn as meshes
    /// with per-vertex colour - the sky as the prototype's radial gradient
    /// (120% x 70% at 50% 8%, three stops), the ground as its vertical one.
    ///
    /// Colours come from Lobby.uss custom properties (--backdrop-sky-*, --backdrop-ground-*),
    /// so the stylesheet stays the single place a colour is decided. The ground
    /// edge line and the props are ordinary child elements, styled there too.
    ///
    /// TODO(scene): replace with the real lobby scene and a transparent root.
    /// </summary>
    [UxmlElement]
    public partial class LobbyBackdrop : VisualElement
    {
        // Measured from the prototype render: the ground's top edge sits at 75%
        // of the phone's height, after its CSS perspective transform.
        private const float GroundTop = 0.75f;

        private const int SkyColumns = 12;
        private const int SkyRows = 24;

        private static readonly CustomStyleProperty<Color> SkyInner = new CustomStyleProperty<Color>("--backdrop-sky-0");
        private static readonly CustomStyleProperty<Color> SkyMid = new CustomStyleProperty<Color>("--backdrop-sky-1");
        private static readonly CustomStyleProperty<Color> SkyOuter = new CustomStyleProperty<Color>("--backdrop-sky-2");
        private static readonly CustomStyleProperty<Color> GroundHigh = new CustomStyleProperty<Color>("--backdrop-ground-top");
        private static readonly CustomStyleProperty<Color> GroundLow = new CustomStyleProperty<Color>("--backdrop-ground-bottom");

        private Color skyInner = new Color32(247, 238, 219, 255);
        private Color skyMid = new Color32(231, 213, 182, 255);
        private Color skyOuter = new Color32(210, 187, 149, 255);
        private Color groundHigh = new Color32(207, 184, 143, 255);
        private Color groundLow = new Color32(188, 161, 115, 255);

        public LobbyBackdrop()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            Color c;
            if (evt.customStyle.TryGetValue(SkyInner, out c)) skyInner = c;
            if (evt.customStyle.TryGetValue(SkyMid, out c)) skyMid = c;
            if (evt.customStyle.TryGetValue(SkyOuter, out c)) skyOuter = c;
            if (evt.customStyle.TryGetValue(GroundHigh, out c)) groundHigh = c;
            if (evt.customStyle.TryGetValue(GroundLow, out c)) groundLow = c;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;

            DrawSky(context, r);
            DrawGround(context, r);
        }

        private void DrawSky(MeshGenerationContext context, Rect r)
        {
            int cols = SkyColumns + 1;
            int rows = SkyRows + 1;

            Vertex[] vertices = new Vertex[cols * rows];
            ushort[] indices = new ushort[SkyColumns * SkyRows * 6];

            Vector2 centre = new Vector2(r.xMin + r.width * 0.5f, r.yMin + r.height * 0.08f);
            float radiusX = r.width * 1.2f;
            float radiusY = r.height * 0.7f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Vector2 point = new Vector2(
                        r.xMin + r.width * x / SkyColumns,
                        r.yMin + r.height * y / SkyRows);

                    float dx = (point.x - centre.x) / radiusX;
                    float dy = (point.y - centre.y) / radiusY;
                    float t = Mathf.Sqrt(dx * dx + dy * dy);

                    vertices[y * cols + x] = new Vertex
                    {
                        position = new Vector3(point.x, point.y, Vertex.nearZ),
                        tint = SkyAt(t)
                    };
                }
            }

            int i = 0;
            for (int y = 0; y < SkyRows; y++)
            {
                for (int x = 0; x < SkyColumns; x++)
                {
                    ushort a = (ushort)(y * cols + x);
                    ushort b = (ushort)(a + 1);
                    ushort c = (ushort)(a + cols);
                    ushort d = (ushort)(c + 1);

                    indices[i++] = a; indices[i++] = b; indices[i++] = c;
                    indices[i++] = b; indices[i++] = d; indices[i++] = c;
                }
            }

            MeshWriteData mesh = context.Allocate(vertices.Length, indices.Length);
            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(indices);
        }

        private Color SkyAt(float t)
        {
            // Stops at 0%, 46%, 100% of the gradient ray.
            if (t <= 0.46f) return Color.Lerp(skyInner, skyMid, t / 0.46f);
            return Color.Lerp(skyMid, skyOuter, Mathf.Clamp01((t - 0.46f) / 0.54f));
        }

        private void DrawGround(MeshGenerationContext context, Rect r)
        {
            float top = r.yMin + r.height * GroundTop;
            float bottom = r.yMax;

            Vertex[] vertices =
            {
                new Vertex { position = new Vector3(r.xMin, top, Vertex.nearZ), tint = groundHigh },
                new Vertex { position = new Vector3(r.xMax, top, Vertex.nearZ), tint = groundHigh },
                new Vertex { position = new Vector3(r.xMin, bottom, Vertex.nearZ), tint = groundLow },
                new Vertex { position = new Vector3(r.xMax, bottom, Vertex.nearZ), tint = groundLow },
            };

            ushort[] indices = { 0, 1, 2, 1, 3, 2 };

            MeshWriteData mesh = context.Allocate(vertices.Length, indices.Length);
            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(indices);
        }
    }
}
