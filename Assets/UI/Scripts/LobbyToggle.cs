using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The one binary control: a 48x28 track with a 20 px knob that springs between the two inner
    /// edges. It takes no taps itself; the row that carries it calls <see cref="Flip"/>. The tick
    /// runs only while the spring is moving.
    /// </summary>
    [UxmlElement]
    public sealed partial class LobbyToggle : VisualElement
    {
        private readonly VisualElement knob = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly IVisualElementScheduledItem ticker;
        private LobbyMotion.Spring motion = new LobbyMotion.Spring(LobbyToggleMath.KnobX(false));
        private bool value;
        private double lastTime;
        public event Action<bool> Changed;

        [UxmlAttribute]
        public bool Value
        {
            get => value;
            set { if (this.value == value) return; SetValueWithoutNotify(value); Changed?.Invoke(value); }
        }

        public LobbyToggle() : this(false) { }

        public LobbyToggle(bool initial)
        {
            AddToClassList("lb-toggle"); knob.AddToClassList("lb-toggle__knob"); Add(knob);
            pickingMode = PickingMode.Ignore;
            value = initial; EnableInClassList("lb-toggle--on", initial);
            motion = new LobbyMotion.Spring(LobbyToggleMath.KnobX(initial)); Paint();
            ticker = schedule.Execute(Tick).Every(16); ticker.Pause();
            RegisterCallback<AttachToPanelEvent>(_ => { if (!motion.Settled) Wake(); });
        }

        public void SetValueWithoutNotify(bool next)
        {
            value = next; EnableInClassList("lb-toggle--on", next);
            motion.Target = LobbyToggleMath.KnobX(next);
            if (panel == null) { motion = new LobbyMotion.Spring(motion.Target); Paint(); }
            else if (!motion.Settled) Wake();
        }

        public void Flip() => Value = !value;

        private void Wake() { lastTime = Time.realtimeSinceStartupAsDouble; ticker.Resume(); }

        private void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime); lastTime = now;
            if (!motion.Settled) { motion.Step(dt, 1f); Paint(); }
            if (motion.Settled) { motion.Position = motion.Target; motion.Velocity = 0f; Paint(); ticker.Pause(); }
        }

        private void Paint() => knob.style.translate = new Translate(motion.Position, 0);
    }
}
