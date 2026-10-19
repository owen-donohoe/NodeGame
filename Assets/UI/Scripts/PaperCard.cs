using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Retained paper geometry. Grain is shared; no animation or repaint timer.</summary>
    [UxmlElement]
    public partial class PaperCard : VisualElement
    {
        private static readonly CustomStyleProperty<Color> Fill = new CustomStyleProperty<Color>("--paper-card-fill");
        private static readonly CustomStyleProperty<Color> Ink = new CustomStyleProperty<Color>("--paper-card-ink");
        private static readonly CustomStyleProperty<Color> Shadow = new CustomStyleProperty<Color>("--paper-card-shadow");
        private static readonly CustomStyleProperty<float> Width = new CustomStyleProperty<float>("--paper-card-width");
        private static readonly CustomStyleProperty<float> OffsetX = new CustomStyleProperty<float>("--paper-card-shadow-x");
        private static readonly CustomStyleProperty<float> OffsetY = new CustomStyleProperty<float>("--paper-card-shadow-y");
        private static readonly CustomStyleProperty<float> Wobble = new CustomStyleProperty<float>("--paper-card-wobble");
        private static readonly CustomStyleProperty<float> GrainOpacity = new CustomStyleProperty<float>("--paper-card-grain");
        private static Texture2D grain;
        private readonly VisualElement swatch;
        private PaperPoint[] outline;
        private Color fill, ink, shadow;
        private float width = 3, offsetX = 2, offsetY = 4, wobble = 0.8f, grainOpacity = 0.045f;
        private string seed;
        private bool rotatePaper = true;
        private bool pressed;

        [UxmlAttribute]
        public string Seed { get => seed; set { seed = value; Rebuild(); } }
        [UxmlAttribute]
        public bool RotatePaper { get => rotatePaper; set { rotatePaper = value; Rebuild(); } }
        public bool Pressed { get => pressed; set { if (pressed == value) return; pressed = value; MarkDirtyRepaint(); } }

        public PaperCard()
        {
            AddToClassList("ui-paper");
            // Resolve aliases through ordinary USS properties as well as customStyle:
            // Unity versions differ on custom properties which alias other tokens.
            swatch = new VisualElement { pickingMode = PickingMode.Ignore };
            swatch.AddToClassList("ui-paper__swatch");
            hierarchy.Add(swatch);
            swatch.RegisterCallback<CustomStyleResolvedEvent>(_ => Resolve());
            RegisterCallback<GeometryChangedEvent>(_ => Rebuild());
            RegisterCallback<CustomStyleResolvedEvent>(_ => Resolve());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (string.IsNullOrEmpty(name)) name = "paper-" + string.Join("-", GetClasses()) + "-" + (parent?.IndexOf(this) ?? 0);
                Rebuild();
            });
            RegisterCallback<PointerDownEvent>(_ => { if (ClassListContains("ui-paper--button")) Pressed = true; });
            RegisterCallback<PointerUpEvent>(_ => Pressed = false);
            RegisterCallback<PointerCancelEvent>(_ => Pressed = false);
            generateVisualContent += Draw;
        }

        private void Resolve()
        {
            if (!customStyle.TryGetValue(Fill, out fill)) fill = swatch.resolvedStyle.backgroundColor;
            if (!customStyle.TryGetValue(Ink, out ink)) ink = swatch.resolvedStyle.color;
            if (!customStyle.TryGetValue(Shadow, out shadow)) shadow = swatch.resolvedStyle.borderLeftColor;
            width = customStyle.TryGetValue(Width, out float w) ? w : swatch.resolvedStyle.borderLeftWidth;
            offsetX = customStyle.TryGetValue(OffsetX, out float x) ? x : swatch.resolvedStyle.borderRightWidth;
            offsetY = customStyle.TryGetValue(OffsetY, out float y) ? y : swatch.resolvedStyle.borderBottomWidth;
            wobble = customStyle.TryGetValue(Wobble, out float a) ? a : swatch.resolvedStyle.borderTopWidth;
            grainOpacity = customStyle.TryGetValue(GrainOpacity, out float g) ? g : swatch.resolvedStyle.opacity;
            Rebuild();
        }

        private void Rebuild()
        {
            if (swatch.parent != this) hierarchy.Insert(0, swatch);
            Rect rect = new Rect(0, 0, resolvedStyle.width, resolvedStyle.height);
            if (rect.width <= 8 || rect.height <= 8) return;
            uint hash = PaperMath.Seed(string.IsNullOrEmpty(seed) ? name : seed);
            float inset = Mathf.Max(2, width * 0.5f + wobble);
            float radius = Mathf.Max(8, resolvedStyle.borderTopLeftRadius);
            outline = PaperMath.Outline(rect.width - inset * 2, rect.height - inset * 2, radius, wobble, hash);
            style.rotate = new Rotate(new Angle(rotatePaper ? PaperMath.Rotation(hash) : 0));
            MarkDirtyRepaint();
        }

        private void Path(Painter2D painter, Vector2 offset)
        {
            painter.BeginPath();
            for (int i = 0; i < outline.Length - 1; i++)
            {
                var point = new Vector2(outline[i].X, outline[i].Y) + offset;
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (outline == null) return;
            float inset = Mathf.Max(2, width * 0.5f + wobble);
            Vector2 origin = new Vector2(inset, inset);
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            if (!pressed)
            {
                Path(p, origin + new Vector2(offsetX, offsetY));
                p.fillColor = shadow; p.Fill();
                p.lineWidth = width + 2; p.strokeColor = new Color(shadow.r, shadow.g, shadow.b, shadow.a * 0.3f); p.Stroke();
            }
            Path(p, origin); p.fillColor = fill; p.Fill();
            DrawGrain(context, origin);
            if (width > 0) { Path(p, origin); p.strokeColor = ink; p.lineWidth = width; p.Stroke(); }
        }

        private void DrawGrain(MeshGenerationContext context, Vector2 origin)
        {
            if (grainOpacity <= 0 || fill.a <= 0) return;
            if (grain == null)
            {
                grain = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Shared paper grain", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var pixels = new Color32[64 * 64];
                uint state = 1013;
                for (int i = 0; i < pixels.Length; i++)
                {
                    state = unchecked(state * 1664525u + 1013904223u);
                    pixels[i] = new Color32(0, 0, 0, (byte)(state >> 24));
                }
                grain.SetPixels32(pixels); grain.Apply(false, true);
            }
            int count = outline.Length - 1;
            MeshWriteData mesh = context.Allocate(count + 1, count * 3, grain);
            Vector2 center = new Vector2(resolvedStyle.width, resolvedStyle.height) * 0.5f;
            Add(center);
            for (int i = 0; i < count; i++) Add(origin + new Vector2(outline[i].X, outline[i].Y));
            for (int i = 0; i < count; i++)
            { mesh.SetNextIndex(0); mesh.SetNextIndex((ushort)(i + 1)); mesh.SetNextIndex((ushort)((i + 1) % count + 1)); }
            void Add(Vector2 point)
            {
                mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ),
                    tint = new Color(ink.r, ink.g, ink.b, grainOpacity), uv = point / 64f });
            }
        }
    }
}
