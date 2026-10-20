using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One retargetable tween. Inline transforms never compete with USS transitions.</summary>
    internal sealed class LobbyElementMotion
    {
        private readonly VisualElement element;
        private readonly VisualElement scrim;
        private readonly IVisualElementScheduledItem tick;
        private float opacity, x, scale = 1f;
        private float fromOpacity, fromX, fromScale, toOpacity, toX, toScale;
        private double started;
        private float duration;
        private bool closing, running;
        private Action completed;
        public float Offset { get { Sample(); return x; } }

        public LobbyElementMotion(VisualElement element, VisualElement scrim = null)
        {
            this.element = element; this.scrim = scrim;
            element.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0) };
            if (scrim != null) scrim.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0) };
            tick = element.schedule.Execute(Sample).Every(1); tick.Pause();
        }
        public void Set(float alpha, float offset, float size)
        {
            running = false; tick.Pause(); completed = null;
            opacity = alpha; x = offset; scale = size; Apply();
        }
        public void Hold()
        {
            completed = null; Sample(); running = false; tick.Pause();
        }
        public void To(float alpha, float offset, float size, float seconds, bool easeIn = false, Action done = null)
        {
            completed = null;
            Sample();
            fromOpacity = opacity; fromX = x; fromScale = scale;
            toOpacity = alpha; toX = offset; toScale = size;
            duration = seconds; started = Time.unscaledTimeAsDouble;
            closing = easeIn; completed = done; running = true; tick.Resume();
        }
        public void Open()
        {
            To(1f, 0f, 1f, LobbyMotion.OpenSeconds);
        }
        public void Close(Action done)
        {
            To(0f, 0f, LobbyMotion.ClosedScale, LobbyMotion.CloseSeconds, true, done);
        }
        private void Sample()
        {
            if (!running) return;
            float progress = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - started) / duration);
            float t = closing ? LobbyMotion.InCubic(progress) : LobbyMotion.OutCubic(progress);
            opacity = Mathf.Lerp(fromOpacity, toOpacity, t);
            x = Mathf.Lerp(fromX, toX, t); scale = Mathf.Lerp(fromScale, toScale, t); Apply();
            if (progress < 1f) return;
            running = false; tick.Pause();
            var done = completed; completed = null; done?.Invoke();
        }
        private void Apply()
        {
            element.style.opacity = opacity;
            element.style.translate = new Translate(x, 0);
            element.style.scale = new Scale(Vector3.one * scale);
            if (scrim != null) scrim.style.opacity = opacity;
        }
    }
}
