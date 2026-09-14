using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Which glyph a <see cref="LobbyIcon"/> draws.</summary>
    public enum LobbyIconKind
    {
        None,
        Shop,     // prototype: ⌂
        Spark,    // prototype: ✦
        Tools,    // prototype: ⚒
        Smile,    // prototype: ☺
        Gear,     // prototype: ⚙
        Envelope, // prototype: ✉
        Mouth     // the villager's smile: a CSS bottom border with radii, which USS draws flat
    }

    /// <summary>
    /// A small vector glyph for the lobby.
    ///
    /// The prototype uses Unicode symbols and emoji, which Fredoka does not
    /// carry. Rather than add a second fallback font for seven glyphs, each is
    /// drawn once with Painter2D into a white VectorImage and shown as the
    /// element's background image. Colour is then
    /// -unity-background-image-tint-color, set in Lobby.uss - an ordinary style
    /// property, so a tab's icon colour transitions with the tab like any other.
    ///
    /// Size comes from USS width/height; the image scales to fit.
    ///
    /// TODO(art): these stand in for icon sprites that do not exist yet.
    /// </summary>
    [UxmlElement]
    public partial class LobbyIcon : VisualElement
    {
        // Design space. Every glyph is drawn inside a 24x24 box.
        private const float Box = 24f;

        private static readonly Dictionary<LobbyIconKind, VectorImage> cache =
            new Dictionary<LobbyIconKind, VectorImage>();

        private LobbyIconKind kind;

        [UxmlAttribute]
        public LobbyIconKind Kind
        {
            get { return kind; }
            set
            {
                kind = value;
                style.backgroundImage = new StyleBackground(ImageFor(kind));
            }
        }

        public LobbyIcon()
        {
            AddToClassList("lb-icon");
            pickingMode = PickingMode.Ignore;
        }

        public LobbyIcon(LobbyIconKind kind) : this()
        {
            Kind = kind;
        }

        private static VectorImage ImageFor(LobbyIconKind kind)
        {
            if (kind == LobbyIconKind.None) return null;

            VectorImage image;
            if (cache.TryGetValue(kind, out image) && image != null) return image;

            Painter2D p = new Painter2D();
            p.fillColor = Color.white;
            p.strokeColor = Color.white;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // Two transparent corner dots pin the image bounds to the full
            // design box, so every glyph keeps the same scale and centre.
            p.fillColor = new Color(1f, 1f, 1f, 0f);
            p.BeginPath(); p.Arc(new Vector2(0.01f, 0.01f), 0.01f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(new Vector2(Box - 0.01f, Box - 0.01f), 0.01f, 0f, 360f); p.Fill();
            p.fillColor = Color.white;

            switch (kind)
            {
                case LobbyIconKind.Shop: DrawShop(p); break;
                case LobbyIconKind.Spark: DrawSpark(p); break;
                case LobbyIconKind.Tools: DrawTools(p); break;
                case LobbyIconKind.Smile: DrawSmile(p); break;
                case LobbyIconKind.Gear: DrawGear(p); break;
                case LobbyIconKind.Envelope: DrawEnvelope(p); break;
                case LobbyIconKind.Mouth: DrawMouth(p); break;
            }

            image = ScriptableObject.CreateInstance<VectorImage>();
            image.hideFlags = HideFlags.DontSave;
            p.SaveToVectorImage(image);
            p.Dispose();

            cache[kind] = image;
            return image;
        }

        // ⌂ - a house outline.
        private static void DrawShop(Painter2D p)
        {
            p.lineWidth = 2.2f;
            p.BeginPath();
            p.MoveTo(new Vector2(4f, 21f));
            p.LineTo(new Vector2(4f, 10.5f));
            p.LineTo(new Vector2(12f, 3.5f));
            p.LineTo(new Vector2(20f, 10.5f));
            p.LineTo(new Vector2(20f, 21f));
            p.ClosePath();
            p.Stroke();
        }

        // ✦ - a four-pointed star with concave sides.
        private static void DrawSpark(Painter2D p)
        {
            Vector2 c = new Vector2(12f, 12f);
            p.BeginPath();
            p.MoveTo(new Vector2(12f, 1f));
            p.QuadraticCurveTo(c + new Vector2(1.6f, -1.6f), new Vector2(23f, 12f));
            p.QuadraticCurveTo(c + new Vector2(1.6f, 1.6f), new Vector2(12f, 23f));
            p.QuadraticCurveTo(c + new Vector2(-1.6f, 1.6f), new Vector2(1f, 12f));
            p.QuadraticCurveTo(c + new Vector2(-1.6f, -1.6f), new Vector2(12f, 1f));
            p.ClosePath();
            p.Fill();
        }

        // ⚒ - a hammer and a pick, crossed.
        private static void DrawTools(Painter2D p)
        {
            p.lineWidth = 2.2f;

            // Handles.
            p.BeginPath();
            p.MoveTo(new Vector2(5f, 21f));
            p.LineTo(new Vector2(17f, 7f));
            p.MoveTo(new Vector2(19f, 21f));
            p.LineTo(new Vector2(7f, 7f));
            p.Stroke();

            // Hammer head, on the handle rising to the right.
            p.BeginPath();
            p.MoveTo(new Vector2(12.5f, 3.5f));
            p.LineTo(new Vector2(17.5f, 2f));
            p.LineTo(new Vector2(21.5f, 6.5f));
            p.LineTo(new Vector2(20f, 11f));
            p.ClosePath();
            p.Fill();

            // Pick head, curved, on the handle rising to the left.
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(2.5f, 9.5f));
            p.QuadraticCurveTo(new Vector2(4f, 3f), new Vector2(10.5f, 2.5f));
            p.Stroke();
        }

        // ☺ - a face.
        private static void DrawSmile(Painter2D p)
        {
            p.lineWidth = 2f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 12f), 10f, 0f, 360f);
            p.Stroke();

            p.BeginPath(); p.Arc(new Vector2(8.6f, 9.5f), 1.5f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(new Vector2(15.4f, 9.5f), 1.5f, 0f, 360f); p.Fill();

            p.lineWidth = 1.9f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 12.5f), 5.2f, 25f, 155f);
            p.Stroke();
        }

        // ⚙ - eight teeth on a ring.
        private static void DrawGear(Painter2D p)
        {
            Vector2 c = new Vector2(12f, 12f);
            const int teeth = 8;
            const float outer = 11f;
            const float inner = 8.2f;
            const float halfTooth = 12f;   // degrees either side of a tooth centre, at the tip
            const float halfRoot = 17f;    // degrees either side, at the root

            p.BeginPath();
            for (int i = 0; i < teeth; i++)
            {
                float a = i * 360f / teeth;
                Vector2 r0 = Polar(c, inner, a - halfRoot);
                Vector2 t0 = Polar(c, outer, a - halfTooth);
                Vector2 t1 = Polar(c, outer, a + halfTooth);
                Vector2 r1 = Polar(c, inner, a + halfRoot);

                if (i == 0) p.MoveTo(r0); else p.LineTo(r0);
                p.LineTo(t0);
                p.LineTo(t1);
                p.LineTo(r1);
            }
            p.ClosePath();

            // The hole is a second sub-path in the same path; even-odd filling
            // leaves it empty.
            p.MoveTo(c + new Vector2(3.6f, 0f));
            p.Arc(c, 3.6f, 0f, 360f);
            p.ClosePath();
            p.Fill(FillRule.OddEven);
        }

        // ✉ - an envelope.
        private static void DrawEnvelope(Painter2D p)
        {
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(3f, 6f));
            p.LineTo(new Vector2(21f, 6f));
            p.LineTo(new Vector2(21f, 18.5f));
            p.LineTo(new Vector2(3f, 18.5f));
            p.ClosePath();
            p.Stroke();

            p.BeginPath();
            p.MoveTo(new Vector2(3.5f, 6.5f));
            p.LineTo(new Vector2(12f, 13f));
            p.LineTo(new Vector2(20.5f, 6.5f));
            p.Stroke();
        }

        // The villager's smile, in the top half of the box so a square element
        // can be placed by its top edge.
        private static void DrawMouth(Painter2D p)
        {
            p.lineWidth = 2.8f;
            p.BeginPath();
            p.MoveTo(new Vector2(2.5f, 3f));
            p.QuadraticCurveTo(new Vector2(12f, 13.5f), new Vector2(21.5f, 3f));
            p.Stroke();
        }

        private static Vector2 Polar(Vector2 centre, float radius, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;
        }
    }
}
