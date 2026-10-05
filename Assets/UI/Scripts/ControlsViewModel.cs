using System.Collections.Generic;

namespace NodeWar.Lobby
{
    /// <summary>One line of the Controls list, as the page draws it.</summary>
    public readonly struct ControlsRow
    {
        public readonly InputSlot Slot;
        public readonly string Label;
        public readonly bool Enabled;

        /// <summary>A locked slot has no switch; the page shows <see cref="StateText"/> instead.</summary>
        public readonly bool Locked;

        /// <summary>"always" for a locked row, otherwise "On" or "Off".</summary>
        public readonly string StateText;

        public readonly string ActionLabel;

        /// <summary>More than one action is allowed, so the action is a choice (a dropdown) and not plain text.</summary>
        public readonly bool ActionCycles;

        /// <summary>The action as a value, for picking it in a list.</summary>
        public readonly InputAction Action;

        /// <summary>Only this slot's allowed actions, in the order the list shows them.</summary>
        public readonly IReadOnlyList<InputAction> Options;

        /// <summary>The option labels, same order as <see cref="Options"/>.</summary>
        public readonly IReadOnlyList<string> OptionLabels;

        /// <summary>Index of the current action in <see cref="Options"/>, for the open list's darker item.</summary>
        public readonly int SelectedIndex;

        public ControlsRow(InputSlot slot, string label, bool enabled, bool locked,
            string stateText, string actionLabel, bool actionCycles, InputAction action,
            IReadOnlyList<InputAction> options, IReadOnlyList<string> optionLabels, int selectedIndex)
        {
            Slot = slot;
            Label = label;
            Enabled = enabled;
            Locked = locked;
            StateText = stateText;
            ActionLabel = actionLabel;
            ActionCycles = actionCycles;
            Action = action;
            Options = options;
            OptionLabels = optionLabels;
            SelectedIndex = selectedIndex;
        }

        /// <summary>A dropdown only when there is something to choose between.</summary>
        public bool HasDropdown { get { return ActionCycles; } }

        /// <summary>A switched-off slot is drawn dimmed as a whole; its checkbox stays live.</summary>
        public bool Dimmed { get { return !Enabled; } }
    }

    /// <summary>
    /// What the Controls section shows and what each tap does to the settings,
    /// kept free of UnityEngine so the rules (a locked row cannot be switched
    /// off, cycling lands only on allowed actions, reset restores the defaults)
    /// are tested rather than trusted. The page and the in-match panel both
    /// call these and own nothing but the elements.
    ///
    /// Every edit returns a new value and clones <c>inputBindings</c> first. A
    /// GameSettingsData copy aliases the array, so editing in place would make
    /// the edited value and the saved one the same array and
    /// <see cref="GameSettingsData.Differ"/> would never see the change.
    /// </summary>
    public static class ControlsViewModel
    {
        /// <summary>A header and the slots under it.</summary>
        public readonly struct ControlsGroup
        {
            public readonly string Title;
            public readonly IReadOnlyList<InputSlot> Slots;
            public ControlsGroup(string title, params InputSlot[] slots)
            {
                Title = title;
                Slots = slots;
            }
        }

        /// <summary>Slots grouped by the kind of gesture, in the order the panel lists them.</summary>
        public static readonly IReadOnlyList<ControlsGroup> Groups = new[]
        {
            new ControlsGroup("Tap", InputSlot.TapVillager, InputSlot.DoubleTapGround, InputSlot.DoubleTapVillager),
            new ControlsGroup("Drag", InputSlot.Drag, InputSlot.HoldDrag, InputSlot.DragFromVillager,
                InputSlot.DoubleTapDrag),
            new ControlsGroup("Hold", InputSlot.Hold),
            new ControlsGroup("Two fingers", InputSlot.TwoFingerDrag, InputSlot.Pinch, InputSlot.TwoFingerTap),
            new ControlsGroup("Mouse", InputSlot.MiddleDrag, InputSlot.ScrollWheel)
        };

        /// <summary>Shown on a child row while its parent (the camera button) is off.</summary>
        public const string CameraChildNote = "Needs Camera button";

        public const string CameraAndLayoutTitle = "Camera, layout & hints";

        private static readonly InputSlot[] RowOrder = FlattenGroups();

        private static InputSlot[] FlattenGroups()
        {
            var order = new List<InputSlot>();
            foreach (ControlsGroup group in Groups) order.AddRange(group.Slots);
            return order.ToArray();
        }

        /// <summary>Every slot once, in group order.</summary>
        public static IReadOnlyList<InputSlot> Slots { get { return RowOrder; } }

        public static string SlotLabel(InputSlot slot)
        {
            switch (slot)
            {
                case InputSlot.TapVillager: return "Tap a villager";
                case InputSlot.HoldDrag: return "Hold + drag";
                case InputSlot.Drag: return "Drag";
                case InputSlot.DragFromVillager: return "Drag from a villager";
                case InputSlot.TwoFingerDrag: return "Two-finger drag";
                case InputSlot.Pinch: return "Pinch";
                case InputSlot.DoubleTapGround: return "Double-tap ground";
                case InputSlot.DoubleTapVillager: return "Double-tap a villager";
                case InputSlot.TwoFingerTap: return "Two-finger tap";
                case InputSlot.DoubleTapDrag: return "Double-tap + drag";
                case InputSlot.Hold: return "Hold (no drag)";
                case InputSlot.MiddleDrag: return "Middle-drag (mouse)";
                case InputSlot.ScrollWheel: return "Scroll wheel (mouse)";
                default: return slot.ToString();
            }
        }

        public static string ActionLabel(InputAction action)
        {
            switch (action)
            {
                case InputAction.AddRemove: return "Add / remove";
                case InputAction.Replace: return "Replace";
                case InputAction.LassoSelect: return "Lasso select";
                case InputAction.Pan: return "Pan camera";
                case InputAction.Order: return "Order";
                case InputAction.Zoom: return "Zoom";
                case InputAction.ReturnToCore: return "Return to core";
                case InputAction.SelectAllIdle: return "Select all idle";
                case InputAction.ToggleFitDefaultZoom: return "Fit / default zoom";
                case InputAction.SelectAllOnNode: return "Select all on node";
                case InputAction.ClearSelection: return "Clear selection";
                case InputAction.OpenInfo: return "Open info";
                default: return action.ToString();
            }
        }

        public static string WarningText(InputBindingWarning warning)
        {
            switch (warning)
            {
                case InputBindingWarning.NoPan:
                    return "Nothing pans the camera. The camera button still recentres.";
                case InputBindingWarning.NoZoom:
                    return "Nothing zooms the camera.";
                case InputBindingWarning.DragAndHoldDrag:
                    return "Drag and Hold + drag are told apart by timing: a slow drag can read as a hold.";
                case InputBindingWarning.RedundantAction:
                    return "Two inputs do the same thing.";
                default:
                    return warning.ToString();
            }
        }

        /// <summary>One line per warning, empty when there are none.</summary>
        public static string WarningsText(GameSettingsData settings)
        {
            List<InputBindingWarning> warnings = InputBindings.CheckWarnings(settings);
            if (warnings.Count == 0) return string.Empty;

            var lines = new string[warnings.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = "⚠ " + WarningText(warnings[i]);
            return string.Join("\n", lines);
        }

        public static ControlsRow RowFor(GameSettingsData settings, InputSlot slot)
        {
            InputBinding[] bindings = GameSettingsData.Normalized(settings).inputBindings;
            InputBinding binding = bindings[(int)slot];
            InputSlotDefinition definition = InputBindings.DefinitionFor(slot);
            bool enabled = definition.Locked || binding.enabled;
            var labels = new string[definition.AllowedActions.Count];
            int selected = -1;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = ActionLabel(definition.AllowedActions[i]);
                if ((int)definition.AllowedActions[i] == binding.action) selected = i;
            }
            return new ControlsRow(slot, SlotLabel(slot), enabled, definition.Locked,
                definition.Locked ? "always" : (enabled ? "On" : "Off"),
                ActionLabel((InputAction)binding.action),
                definition.AllowedActions.Count > 1, (InputAction)binding.action,
                definition.AllowedActions, labels, selected);
        }

        public static ControlsRow[] Rows(GameSettingsData settings)
        {
            var rows = new ControlsRow[RowOrder.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = RowFor(settings, RowOrder[i]);
            return rows;
        }

        /// <summary>
        /// The next allowed action after <paramref name="current"/>, wrapping.
        /// A stored value that is not allowed for the slot (a hand-edited save)
        /// goes to the slot's default rather than to a neighbour.
        /// </summary>
        public static InputAction NextAction(InputSlot slot, InputAction current)
        {
            InputSlotDefinition definition = InputBindings.DefinitionFor(slot);
            IReadOnlyList<InputAction> allowed = definition.AllowedActions;
            for (int i = 0; i < allowed.Count; i++)
                if (allowed[i] == current) return allowed[(i + 1) % allowed.Count];
            return definition.DefaultAction;
        }

        public static GameSettingsData ToggleSlot(GameSettingsData settings, InputSlot slot)
        {
            settings = Editable(settings);
            if (InputBindings.DefinitionFor(slot).Locked) return settings;
            settings.inputBindings[(int)slot].enabled = !settings.inputBindings[(int)slot].enabled;
            return settings;
        }

        public static GameSettingsData CycleAction(GameSettingsData settings, InputSlot slot)
        {
            settings = Editable(settings);
            InputBinding binding = settings.inputBindings[(int)slot];
            binding.action = (int)NextAction(slot, (InputAction)binding.action);
            settings.inputBindings[(int)slot] = binding;
            return settings;
        }

        public static GameSettingsData ToggleCameraButton(GameSettingsData settings)
        {
            settings = Editable(settings);
            settings.showCameraButton = !settings.showCameraButton;
            return settings;
        }

        /// <summary>
        /// Rows that belong to the camera button (Drag to zoom) are faded and inert while it is
        /// off: a third state, distinct from their own off (empty outline) and on (filled).
        /// </summary>
        public static bool CameraChildFaded(GameSettingsData settings)
        {
            return !GameSettingsData.Normalized(settings).showCameraButton;
        }

        public static bool CameraZoomDimmed(GameSettingsData settings)
        {
            return CameraChildFaded(settings);
        }

        /// <summary>Picks an action from a slot's list. A value the slot does not allow changes nothing.</summary>
        public static GameSettingsData SetAction(GameSettingsData settings, InputSlot slot, InputAction action)
        {
            settings = Editable(settings);
            if (!InputBindings.IsAllowed(slot, action)) return settings;
            InputBinding binding = settings.inputBindings[(int)slot];
            binding.action = (int)action;
            settings.inputBindings[(int)slot] = binding;
            return settings;
        }

        public static GameSettingsData ToggleCameraButtonZoom(GameSettingsData settings)
        {
            settings = Editable(settings);
            if (!settings.showCameraButton) return settings;
            settings.cameraButtonZoom = !settings.cameraButtonZoom;
            return settings;
        }

        public static GameSettingsData ToggleSelectionBar(GameSettingsData settings)
        {
            settings = Editable(settings);
            settings.showSelectionBar = !settings.showSelectionBar;
            return settings;
        }

        public static GameSettingsData ToggleTooltips(GameSettingsData settings)
        {
            settings = Editable(settings);
            settings.tooltips = !settings.tooltips;
            return settings;
        }

        public static GameSettingsData CycleControlsSide(GameSettingsData settings)
        {
            settings = Editable(settings);
            settings.controlsSide = settings.controlsSide == 1 ? 0 : 1;
            return settings;
        }

        public static GameSettingsData SetHoldTime(GameSettingsData settings, float seconds)
        {
            settings = Editable(settings);
            settings.holdTime = seconds;
            return GameSettingsData.Normalized(settings);
        }

        /// <summary>
        /// The controls fields back to <see cref="GameSettingsData.CreateDefault"/>
        /// and nothing else: volumes, accessibility and the rest are not controls.
        /// </summary>
        public static GameSettingsData ResetControls(GameSettingsData settings)
        {
            settings = GameSettingsData.Normalized(settings);
            GameSettingsData defaults = GameSettingsData.CreateDefault();
            settings.inputBindings = defaults.inputBindings;
            settings.holdTime = defaults.holdTime;
            settings.showCameraButton = defaults.showCameraButton;
            settings.cameraButtonZoom = defaults.cameraButtonZoom;
            settings.controlsSide = defaults.controlsSide;
            settings.showSelectionBar = defaults.showSelectionBar;
            settings.tooltips = defaults.tooltips;
            return settings;
        }

        public static string SideLabel(int controlsSide)
        {
            return controlsSide == 1 ? "Left" : "Right";
        }

        public static string HoldTimeLabel(float seconds)
        {
            return seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
        }

        // Normalised, with a private copy of the array so the caller's value is untouched.
        private static GameSettingsData Editable(GameSettingsData settings)
        {
            settings = GameSettingsData.Normalized(settings);
            settings.inputBindings = (InputBinding[])settings.inputBindings.Clone();
            return settings;
        }
    }
}
