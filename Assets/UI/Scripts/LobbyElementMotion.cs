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
        private readonly bool vertical;
        private readonly IVisualElementScheduledItem tick;
        private float opacity, x, scale = 1f;
        private float fromOpacity, fromX, fromScale, toOpacity, toX, toScale;
        private double started;
        private float duration;
        private bool closing, running;
        private Action completed;
        private bool springOpen;
        private double sampledAt;
        private LobbyMotion.Spring offsetSpring, scaleSpring;
        public float Offset { get { Sample(); return x; } }

        public LobbyElementMotion(VisualElement element, VisualElement scrim = null, bool vertical = false)
        {
            this.element = element; this.scrim = scrim; this.vertical = vertical;
            element.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0) };
            if (scrim != null) scrim.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0) };
            tick = element.schedule.Execute(Sample).Every(1); tick.Pause();
        }
        public void Set(float alpha, float offset, float size)
        {
            running = false; tick.Pause(); completed = null;
            opacity = alpha; x = offset; scale = size; Apply();
            offsetSpring = new LobbyMotion.Spring(x); scaleSpring = new LobbyMotion.Spring(scale);
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
            springOpen = false;
        }
        public void Open()
        {
            completed = null; Sample();
            if (opacity <= 0.001f)
            {
                scale = LobbyMotion.OpenScale;
                x = vertical ? Mathf.Max(48f, element.layout.height) : 0f;
                offsetSpring = new LobbyMotion.Spring(x); scaleSpring = new LobbyMotion.Spring(scale);
            }
            else
            {
                offsetSpring.Position = x; scaleSpring.Position = scale;
            }
            offsetSpring.Target = 0f; scaleSpring.Target = 1f;
            fromOpacity = opacity; started = sampledAt = Time.unscaledTimeAsDouble;
            springOpen = running = true; closing = false; tick.Resume(); Apply();
        }
        public void Close(Action done)
        {
            To(0f, vertical ? 32f : 0f, LobbyMotion.ClosedScale, LobbyMotion.CloseSeconds, true, done);
        }
        private void Sample()
        {
            if (!running) return;
            if (springOpen)
            {
                double now = Time.unscaledTimeAsDouble;
                float dt = (float)(now - sampledAt); sampledAt = now;
                offsetSpring.Step(dt, LobbyMotion.PopDamping); scaleSpring.Step(dt, LobbyMotion.PopDamping);
                x = offsetSpring.Position; scale = scaleSpring.Position;
                opacity = Mathf.Lerp(fromOpacity, 1f, Mathf.Clamp01((float)(now - started) / LobbyMotion.FadeSeconds));
                Apply();
                if (now - started >= LobbyMotion.OpenSeconds && offsetSpring.Settled && scaleSpring.Settled)
                { x = 0f; scale = opacity = 1f; Apply(); running = false; tick.Pause(); }
                return;
            }
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
            element.style.translate = vertical ? new Translate(0, x) : new Translate(x, 0);
            element.style.scale = new Scale(Vector3.one * scale);
            if (scrim != null) scrim.style.opacity = opacity;
        }
    }
}
