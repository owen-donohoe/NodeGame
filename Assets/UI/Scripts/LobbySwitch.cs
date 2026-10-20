using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The prototype's on/off switch: a pill with a sliding knob.
    ///
    /// Not UI Toolkit's Toggle restyled, because Toggle's checkmark structure
    /// fights the shape at every step and the prototype's switch is two
    /// elements. The whole settings row is the tap target, so this element
    /// itself takes no taps; the row calls <see cref="Flip"/>.
    /// </summary>
    [UxmlElement]
    public partial class LobbySwitch : VisualElement
    {
        private bool value;
        private readonly VisualElement knob;
        private LobbyMotion.Spring motion;
        private bool placed;
        private double lastTime;

        /// <summary>Raised when the value changes through <see cref="Flip"/> or the setter.</summary>
        public event System.Action<bool> Changed;

        [UxmlAttribute]
        public bool Value
        {
            get { return value; }
            set
            {
                if (this.value == value) return;
                this.value = value;
                EnableInClassList("lb-switch--on", value);
                Retarget();
                if (Changed != null) Changed(value);
            }
        }

        public LobbySwitch()
        {
            AddToClassList("lb-switch");
            pickingMode = PickingMode.Ignore;

            knob = new VisualElement();
            knob.AddToClassList("lb-switch__knob");
            knob.pickingMode = PickingMode.Ignore;
            Add(knob);
            RegisterCallback<GeometryChangedEvent>(_ => Retarget());
            knob.RegisterCallback<GeometryChangedEvent>(_ => Retarget());
            RegisterCallback<AttachToPanelEvent>(_ => lastTime = Time.realtimeSinceStartupAsDouble);
            schedule.Execute(() =>
            {
                double now = Time.realtimeSinceStartupAsDouble;
                float dt = (float)(now - lastTime); lastTime = now;
                if (!placed || motion.Settled) return;
                motion.Step(dt, 1f);
                knob.style.translate = new Translate(motion.Position, 0);
            }).Every(16);
        }

        private void Retarget()
        {
            if (knob == null || float.IsNaN(knob.layout.width) || knob.layout.width <= 0) return;
            float travel = Mathf.Max(0, contentRect.width - knob.layout.width - 2 * knob.resolvedStyle.left);
            motion.Target = value ? travel : 0;
            if (!placed)
            {
                motion = new LobbyMotion.Spring(motion.Target); placed = true;
                knob.style.translate = new Translate(motion.Position, 0);
            }
        }

        public void Flip()
        {
            Value = !value;
        }
    }
}
