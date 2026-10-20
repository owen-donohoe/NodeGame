using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The shared on/off toggle: a 48x28 track with a 20 px knob that springs between the two inner
    /// edges. It takes no taps itself; the row that carries it calls <see cref="Flip"/>.
    /// </summary>
    public sealed class LobbyToggle : VisualElement
    {
        private readonly VisualElement knob = new VisualElement { pickingMode = PickingMode.Ignore };
        private LobbyMotion.Spring motion = new LobbyMotion.Spring(LobbyToggleMath.KnobX(false));
        private bool value;
        private double lastTime;
        public event Action<bool> Changed;

        public bool Value
        {
            get => value;
            set { if (this.value == value) return; SetValueWithoutNotify(value); Changed?.Invoke(value); }
        }

        public LobbyToggle(bool initial = false)
        {
            AddToClassList("lb-toggle"); knob.AddToClassList("lb-toggle__knob"); Add(knob);
            pickingMode = PickingMode.Ignore;
            SetValueWithoutNotify(initial); motion = new LobbyMotion.Spring(LobbyToggleMath.KnobX(initial)); Paint();
            RegisterCallback<AttachToPanelEvent>(_ => lastTime = Time.realtimeSinceStartupAsDouble);
            schedule.Execute(Tick).Every(16);
        }

        public void SetValueWithoutNotify(bool next)
        {
            value = next; EnableInClassList("lb-toggle--on", next);
            motion.Target = LobbyToggleMath.KnobX(next);
        }

        public void Flip() => Value = !value;

        private void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime); lastTime = now;
            if (motion.Settled) return;
            motion.Step(dt, 1f); Paint();
        }

        private void Paint() => knob.style.translate = new Translate(motion.Position, 0);
    }
}
