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
                if (Changed != null) Changed(value);
            }
        }

        public LobbySwitch()
        {
            AddToClassList("lb-switch");
            pickingMode = PickingMode.Ignore;

            VisualElement knob = new VisualElement();
            knob.AddToClassList("lb-switch__knob");
            knob.pickingMode = PickingMode.Ignore;
            Add(knob);
        }

        public void Flip()
        {
            Value = !value;
        }
    }
}
