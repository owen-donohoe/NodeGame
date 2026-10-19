using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Cached glyphs use a stable pen drift; callers still draw in 24-point space.</summary>
    internal sealed class InkPen : IDisposable
    {
        private readonly Painter2D painter = new Painter2D();
        private readonly uint seed;
        private bool started;
        public InkPen(uint seed) { this.seed = seed; }
        public Color fillColor { get => painter.fillColor; set => painter.fillColor = value; }
        public Color strokeColor { get => painter.strokeColor; set => painter.strokeColor = value; }
        public float lineWidth { get => painter.lineWidth; set => painter.lineWidth = Mathf.Max(2.8f, value * 1.2f); }
        public LineJoin lineJoin { get => painter.lineJoin; set => painter.lineJoin = value; }
        public LineCap lineCap { get => painter.lineCap; set => painter.lineCap = value; }
        private Vector2 Point(Vector2 point)
        { var p = PaperMath.InkPoint(seed, point.x, point.y); return new Vector2(p.X, p.Y); }
        public void BeginPath() { painter.BeginPath(); started = false; }
        public void MoveTo(Vector2 point) { painter.MoveTo(Point(point)); started = true; }
        public void LineTo(Vector2 point)
        {
            if (!started) { MoveTo(point); return; }
            painter.LineTo(Point(point));
        }
        public void QuadraticCurveTo(Vector2 control, Vector2 end)
            => painter.QuadraticCurveTo(Point(control), Point(end));
        public void ArcTo(Vector2 control, Vector2 end, float radius)
            => painter.ArcTo(Point(control), Point(end), radius);
        public void Arc(Vector2 center, float radius, float start, float end)
        {
            // The two invisible bound pins must remain exact.
            if (radius < 0.1f) { painter.Arc(center, radius, start, end); started = true; return; }
            int count = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(end - start) / 15f));
            for (int i = 0; i <= count; i++)
            {
                float radians = Mathf.Lerp(start, end, i / (float)count) * Mathf.Deg2Rad;
                LineTo(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
        }
        public void ClosePath() => painter.ClosePath();
        public void Stroke() => painter.Stroke();
        public void Fill(FillRule rule = FillRule.NonZero) => painter.Fill(rule);
        public void SaveToVectorImage(VectorImage image) => painter.SaveToVectorImage(image);
        public void Dispose() => painter.Dispose();
    }
}
