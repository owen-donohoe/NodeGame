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
        Mouth,    // the villager's smile: a CSS bottom border with radii, which USS draws flat
        Tv,       // match history
        Back,     // prototype: ←
        Flag,     // prototype: ⚑
        Hat,      // prototype: ◠
        Diamond,  // prototype: ❖
        District, // prototype: 🏛
        Suit,     // prototype: 🥋
        Lock,     // prototype: 🔒
        Pip,      // prototype: ◆
        Close,    // the match sheet's close button: ✕

        // In-match indicators and emotes. Same reason as the rest: Fredoka
        // carries none of these, and a fallback font would differ by platform.
        Alert,    // !  - an enemy headed for something of yours
        Swords,   // ⚔  - a fight
        Capture,  // ↓  - a node being pushed toward the enemy
        Sleep,    // zz - an idle villager
        Respawn,  // ↻  - a villager back at the Core
        Pointer,  // ▶  - the edge arrow, drawn pointing right and rotated
        Frown,    // ☹  - the sad emote
        Angry,    // the angry emote
        Speaker   // emote mute state
    }

    /// <summary>
    /// A small vector glyph for the lobby, and for the in-match HUD's
    /// indicators and emotes, which have the same font problem.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            cache.Clear();
        }

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
                case LobbyIconKind.Tv: DrawTv(p); break;
                case LobbyIconKind.Back: DrawBack(p); break;
                case LobbyIconKind.Flag: DrawFlag(p); break;
                case LobbyIconKind.Hat: DrawHat(p); break;
                case LobbyIconKind.Diamond: DrawDiamond(p); break;
                case LobbyIconKind.District: DrawDistrict(p); break;
                case LobbyIconKind.Suit: DrawSuit(p); break;
                case LobbyIconKind.Lock: DrawLock(p); break;
                case LobbyIconKind.Pip: Diamond(p, 12f, 12f, 10f); break;
                case LobbyIconKind.Close: DrawClose(p); break;
                case LobbyIconKind.Alert: DrawAlert(p); break;
                case LobbyIconKind.Swords: DrawSwords(p); break;
                case LobbyIconKind.Capture: DrawCapture(p); break;
                case LobbyIconKind.Sleep: DrawSleep(p); break;
                case LobbyIconKind.Respawn: DrawRespawn(p); break;
                case LobbyIconKind.Pointer: DrawPointer(p); break;
                case LobbyIconKind.Frown: DrawFrown(p); break;
                case LobbyIconKind.Angry: DrawAngry(p); break;
                case LobbyIconKind.Speaker: DrawSpeaker(p); break;
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

        // A television: screen, antennae, feet.
        private static void DrawTv(Painter2D p)
        {
            p.lineWidth = 2f;
            RoundedRect(p, 2.5f, 7f, 19f, 13f, 2.5f);
            p.Stroke();

            p.BeginPath();
            p.MoveTo(new Vector2(8f, 2.5f));
            p.LineTo(new Vector2(12f, 6.5f));
            p.LineTo(new Vector2(16f, 2.5f));
            p.MoveTo(new Vector2(7f, 20f));
            p.LineTo(new Vector2(6f, 22.5f));
            p.MoveTo(new Vector2(17f, 20f));
            p.LineTo(new Vector2(18f, 22.5f));
            p.Stroke();
        }

        // ← - an arrow pointing left.
        private static void DrawBack(Painter2D p)
        {
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(20f, 12f));
            p.LineTo(new Vector2(4.5f, 12f));
            p.MoveTo(new Vector2(11f, 5f));
            p.LineTo(new Vector2(4f, 12f));
            p.LineTo(new Vector2(11f, 19f));
            p.Stroke();
        }

        // ✕ - two strokes, drawn rather than typed so no font has to carry it.
        private static void DrawClose(Painter2D p)
        {
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(6f, 6f));
            p.LineTo(new Vector2(18f, 18f));
            p.MoveTo(new Vector2(18f, 6f));
            p.LineTo(new Vector2(6f, 18f));
            p.Stroke();
        }

        // ⚑ - a pennant on a pole.
        private static void DrawFlag(Painter2D p)
        {
            p.lineWidth = 2.2f;
            p.BeginPath();
            p.MoveTo(new Vector2(5f, 22f));
            p.LineTo(new Vector2(5f, 2.5f));
            p.Stroke();

            p.BeginPath();
            p.MoveTo(new Vector2(6f, 3f));
            p.LineTo(new Vector2(20f, 7.5f));
            p.LineTo(new Vector2(6f, 12.5f));
            p.ClosePath();
            p.Fill();
        }

        // ◠ - an upper half-circle, the prototype's straw hat.
        private static void DrawHat(Painter2D p)
        {
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(4f, 16f));
            p.QuadraticCurveTo(new Vector2(4f, 6f), new Vector2(12f, 6f));
            p.QuadraticCurveTo(new Vector2(20f, 6f), new Vector2(20f, 16f));
            p.Stroke();
        }

        // ❖ - four diamonds in a diamond.
        private static void DrawDiamond(Painter2D p)
        {
            Diamond(p, 12f, 5f, 4f);
            Diamond(p, 19f, 12f, 4f);
            Diamond(p, 12f, 19f, 4f);
            Diamond(p, 5f, 12f, 4f);
        }

        // 🏛 - a columned building.
        private static void DrawDistrict(Painter2D p)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(2.5f, 8.5f));
            p.LineTo(new Vector2(12f, 2.5f));
            p.LineTo(new Vector2(21.5f, 8.5f));
            p.ClosePath();
            p.Fill();

            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(6f, 11f)); p.LineTo(new Vector2(6f, 18f));
            p.MoveTo(new Vector2(12f, 11f)); p.LineTo(new Vector2(12f, 18f));
            p.MoveTo(new Vector2(18f, 11f)); p.LineTo(new Vector2(18f, 18f));
            p.Stroke();

            p.lineWidth = 2.6f;
            p.BeginPath();
            p.MoveTo(new Vector2(3f, 21f));
            p.LineTo(new Vector2(21f, 21f));
            p.Stroke();
        }

        // 🥋 - a robe with a belt.
        private static void DrawSuit(Painter2D p)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(8f, 3f));
            p.LineTo(new Vector2(12f, 7f));
            p.LineTo(new Vector2(16f, 3f));
            p.LineTo(new Vector2(22f, 7f));
            p.LineTo(new Vector2(19.5f, 11.5f));
            p.LineTo(new Vector2(17.5f, 10.5f));
            p.LineTo(new Vector2(17.5f, 21.5f));
            p.LineTo(new Vector2(6.5f, 21.5f));
            p.LineTo(new Vector2(6.5f, 10.5f));
            p.LineTo(new Vector2(4.5f, 11.5f));
            p.LineTo(new Vector2(2f, 7f));
            p.ClosePath();
            p.Fill();
        }

        // 🔒 - a padlock.
        private static void DrawLock(Painter2D p)
        {
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(7.5f, 11f));
            p.LineTo(new Vector2(7.5f, 8f));
            p.Arc(new Vector2(12f, 8f), 4.5f, 180f, 360f);
            p.LineTo(new Vector2(16.5f, 11f));
            p.Stroke();

            RoundedRect(p, 4f, 10.5f, 16f, 11.5f, 2f);
            p.Fill();
        }

        // ! - a rounded bar over a dot.
        private static void DrawAlert(Painter2D p)
        {
            p.lineWidth = 4.2f;
            p.BeginPath();
            p.MoveTo(new Vector2(12f, 4f));
            p.LineTo(new Vector2(12f, 13.5f));
            p.Stroke();

            p.BeginPath();
            p.Arc(new Vector2(12f, 19.5f), 2.4f, 0f, 360f);
            p.Fill();
        }

        // ⚔ - two swords crossed, hilts down.
        private static void DrawSwords(Painter2D p)
        {
            // Blades.
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(4f, 4f));
            p.LineTo(new Vector2(16f, 16f));
            p.MoveTo(new Vector2(20f, 4f));
            p.LineTo(new Vector2(8f, 16f));
            p.Stroke();

            // Crossguards, square to each blade.
            p.lineWidth = 2.2f;
            p.BeginPath();
            p.MoveTo(new Vector2(13f, 19f));
            p.LineTo(new Vector2(19f, 13f));
            p.MoveTo(new Vector2(5f, 13f));
            p.LineTo(new Vector2(11f, 19f));
            p.Stroke();

            // Grips.
            p.lineWidth = 3f;
            p.BeginPath();
            p.MoveTo(new Vector2(16.5f, 16.5f));
            p.LineTo(new Vector2(20.5f, 20.5f));
            p.MoveTo(new Vector2(7.5f, 16.5f));
            p.LineTo(new Vector2(3.5f, 20.5f));
            p.Stroke();
        }

        // ↓ onto a line - ground being pushed away from you.
        private static void DrawCapture(Painter2D p)
        {
            p.lineWidth = 2.6f;
            p.BeginPath();
            p.MoveTo(new Vector2(12f, 3f));
            p.LineTo(new Vector2(12f, 14.5f));
            p.MoveTo(new Vector2(6.5f, 9.5f));
            p.LineTo(new Vector2(12f, 15f));
            p.LineTo(new Vector2(17.5f, 9.5f));
            p.MoveTo(new Vector2(4f, 20.5f));
            p.LineTo(new Vector2(20f, 20.5f));
            p.Stroke();
        }

        // zz - a large Z and a small one above it.
        private static void DrawSleep(Painter2D p)
        {
            p.lineWidth = 2.3f;
            p.BeginPath();
            p.MoveTo(new Vector2(3.5f, 10f));
            p.LineTo(new Vector2(12.5f, 10f));
            p.LineTo(new Vector2(3.5f, 20.5f));
            p.LineTo(new Vector2(12.5f, 20.5f));
            p.Stroke();

            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(14.5f, 3.5f));
            p.LineTo(new Vector2(20.5f, 3.5f));
            p.LineTo(new Vector2(14.5f, 10.5f));
            p.LineTo(new Vector2(20.5f, 10.5f));
            p.Stroke();
        }

        // ↻ - most of a circle, with a head on its clockwise end.
        private static void DrawRespawn(Painter2D p)
        {
            Vector2 c = new Vector2(12f, 12.5f);
            const float r = 7.5f;
            const float end = 260f;

            p.lineWidth = 2.4f;
            p.BeginPath();
            p.Arc(c, r, -30f, end);
            p.Stroke();

            // Painter2D measures from +x and turns clockwise on screen, so the
            // clockwise tangent at an angle is (-sin, cos).
            float rad = end * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            Vector2 tip = c + radial * r;

            p.BeginPath();
            p.MoveTo(tip + tangent * 4f);
            p.LineTo(tip + radial * 3.6f);
            p.LineTo(tip - radial * 3.6f);
            p.ClosePath();
            p.Fill();
        }

        // Speaker and sound waves, tinted by the current mute state.
        private static void DrawSpeaker(Painter2D p)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(3f, 9f));
            p.LineTo(new Vector2(7f, 9f));
            p.LineTo(new Vector2(12f, 5f));
            p.LineTo(new Vector2(12f, 19f));
            p.LineTo(new Vector2(7f, 15f));
            p.LineTo(new Vector2(3f, 15f));
            p.ClosePath();
            p.Fill();
            p.lineWidth = 2f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 12f), 5f, -45f, 45f);
            p.Stroke();
            p.BeginPath();
            p.Arc(new Vector2(12f, 12f), 9f, -45f, 45f);
            p.Stroke();
        }

        // ▶ - a solid triangle pointing right. The edge arrow rotates it.
        private static void DrawPointer(Painter2D p)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(6f, 4f));
            p.LineTo(new Vector2(20f, 12f));
            p.LineTo(new Vector2(6f, 20f));
            p.ClosePath();
            p.Fill();
        }

        // ☹ - the smile's face with the mouth turned over.
        private static void DrawFrown(Painter2D p)
        {
            p.lineWidth = 2f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 12f), 10f, 0f, 360f);
            p.Stroke();

            p.BeginPath(); p.Arc(new Vector2(8.6f, 9.5f), 1.5f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(new Vector2(15.4f, 9.5f), 1.5f, 0f, 360f); p.Fill();

            p.lineWidth = 1.9f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 20f), 5.2f, 215f, 325f);
            p.Stroke();
        }

        // A face with its brows pulled down and in.
        private static void DrawAngry(Painter2D p)
        {
            p.lineWidth = 2f;
            p.BeginPath();
            p.Arc(new Vector2(12f, 12f), 10f, 0f, 360f);
            p.Stroke();

            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(6.5f, 7f));
            p.LineTo(new Vector2(10.5f, 9.2f));
            p.MoveTo(new Vector2(17.5f, 7f));
            p.LineTo(new Vector2(13.5f, 9.2f));
            p.Stroke();

            p.BeginPath(); p.Arc(new Vector2(8.8f, 11.5f), 1.4f, 0f, 360f); p.Fill();
            p.BeginPath(); p.Arc(new Vector2(15.2f, 11.5f), 1.4f, 0f, 360f); p.Fill();

            p.lineWidth = 1.9f;
            p.BeginPath();
            p.MoveTo(new Vector2(8f, 17.5f));
            p.LineTo(new Vector2(16f, 17.5f));
            p.Stroke();
        }

        private static void Diamond(Painter2D p, float cx, float cy, float r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - r));
            p.LineTo(new Vector2(cx + r, cy));
            p.LineTo(new Vector2(cx, cy + r));
            p.LineTo(new Vector2(cx - r, cy));
            p.ClosePath();
            p.Fill();
        }

        private static void RoundedRect(Painter2D p, float x, float y, float w, float h, float r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r));
            p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h));
            p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r));
            p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
        }

        private static Vector2 Polar(Vector2 centre, float radius, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;
        }
    }
}
