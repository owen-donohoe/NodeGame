using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    // Persisted by index: keep both enums append-only.
    public enum InputSlot
    {
        TapVillager = 0,
        HoldDrag = 1,
        Drag = 2,
        DragFromVillager = 3,
        TwoFingerDrag = 4,
        Pinch = 5,
        DoubleTapGround = 6,
        DoubleTapVillager = 7,
        TwoFingerTap = 8,
        DoubleTapDrag = 9,
        Hold = 10,
        MiddleDrag = 11,
        ScrollWheel = 12
    }

    public enum InputAction
    {
        AddRemove = 0,
        Replace = 1,
        LassoSelect = 2,
        Pan = 3,
        Order = 4,
        Zoom = 5,
        ReturnToCore = 6,
        SelectAllIdle = 7,
        ToggleFitDefaultZoom = 8,
        SelectAllOnNode = 9,
        ClearSelection = 10,
        OpenInfo = 11
    }

    [Serializable]
    public struct InputBinding
    {
        public bool enabled;
        public int action;

        public InputBinding(bool enabled, InputAction action)
        {
            this.enabled = enabled;
            this.action = (int)action;
        }
    }

    public readonly struct InputSlotDefinition
    {
        public readonly bool DefaultEnabled;
        public readonly InputAction DefaultAction;
        public readonly IReadOnlyList<InputAction> AllowedActions;
        public readonly bool Locked;

        public InputSlotDefinition(bool enabled, InputAction action, bool locked,
            params InputAction[] allowed)
        {
            DefaultEnabled = enabled;
            DefaultAction = action;
            Locked = locked;
            AllowedActions = Array.AsReadOnly(allowed);
        }
    }

    public enum InputBindingWarning
    {
        NoPan,
        NoZoom,
        DragAndHoldDrag,
        RedundantAction
    }

    public static class InputBindings
    {
        public const int SlotCount = 13;

        private static readonly InputSlotDefinition[] Slots =
        {
            new InputSlotDefinition(true, InputAction.AddRemove, true, InputAction.AddRemove, InputAction.Replace),
            new InputSlotDefinition(true, InputAction.LassoSelect, false, InputAction.LassoSelect, InputAction.Pan),
            new InputSlotDefinition(true, InputAction.Pan, false, InputAction.Pan, InputAction.LassoSelect),
            new InputSlotDefinition(false, InputAction.Order, false, InputAction.Order),
            new InputSlotDefinition(true, InputAction.Pan, false, InputAction.Pan, InputAction.LassoSelect),
            new InputSlotDefinition(true, InputAction.Zoom, false, InputAction.Zoom),
            new InputSlotDefinition(false, InputAction.ReturnToCore, false,
                InputAction.ReturnToCore, InputAction.SelectAllIdle, InputAction.ToggleFitDefaultZoom),
            new InputSlotDefinition(false, InputAction.SelectAllIdle, false,
                InputAction.SelectAllIdle, InputAction.SelectAllOnNode),
            new InputSlotDefinition(false, InputAction.ClearSelection, false,
                InputAction.ClearSelection, InputAction.ReturnToCore),
            new InputSlotDefinition(false, InputAction.Zoom, false, InputAction.Zoom),
            new InputSlotDefinition(false, InputAction.OpenInfo, false, InputAction.OpenInfo),
            new InputSlotDefinition(true, InputAction.Pan, false, InputAction.Pan),
            new InputSlotDefinition(true, InputAction.Zoom, false, InputAction.Zoom)
        };

        public static InputSlotDefinition DefinitionFor(InputSlot slot)
        {
            return Slots[(int)slot];
        }

        public static InputBinding DefaultFor(InputSlot slot)
        {
            InputSlotDefinition definition = DefinitionFor(slot);
            return new InputBinding(definition.DefaultEnabled, definition.DefaultAction);
        }

        public static bool IsAllowed(InputSlot slot, InputAction action)
        {
            if ((int)slot < 0 || (int)slot >= SlotCount) return false;
            foreach (InputAction allowed in DefinitionFor(slot).AllowedActions)
                if (allowed == action) return true;
            return false;
        }

        public static InputBinding[] CreateDefault()
        {
            var bindings = new InputBinding[SlotCount];
            for (int i = 0; i < bindings.Length; i++)
                bindings[i] = DefaultFor((InputSlot)i);
            return bindings;
        }

        public static InputBinding[] Normalized(InputBinding[] source)
        {
            InputBinding[] bindings = CreateDefault();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (source != null && i < source.Length)
                {
                    bindings[i] = source[i];
                    if (!IsAllowed((InputSlot)i, (InputAction)bindings[i].action))
                        bindings[i].action = (int)Slots[i].DefaultAction;
                }
                if (Slots[i].Locked) bindings[i].enabled = true;
            }
            return bindings;
        }

        public static bool Differ(InputBinding[] a, InputBinding[] b)
        {
            if (ReferenceEquals(a, b)) return false;
            if (a == null || b == null || a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++)
                if (a[i].enabled != b[i].enabled || a[i].action != b[i].action) return true;
            return false;
        }

        public static List<InputBindingWarning> CheckWarnings(GameSettingsData settings)
        {
            settings = GameSettingsData.Normalized(settings);
            InputBinding[] bindings = settings.inputBindings;
            bool pan = false;
            bool zoom = settings.showCameraButton && settings.cameraButtonZoom;
            bool redundant = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (!bindings[i].enabled) continue;
                pan |= bindings[i].action == (int)InputAction.Pan;
                zoom |= bindings[i].action == (int)InputAction.Zoom;
                for (int j = 0; j < i; j++)
                    if (bindings[j].enabled && bindings[j].action == bindings[i].action
                        && PointerFamily((InputSlot)i) == PointerFamily((InputSlot)j))
                        redundant = true;
            }

            var warnings = new List<InputBindingWarning>();
            if (!pan) warnings.Add(InputBindingWarning.NoPan);
            if (!zoom) warnings.Add(InputBindingWarning.NoZoom);
            if (bindings[(int)InputSlot.Drag].enabled && bindings[(int)InputSlot.HoldDrag].enabled)
                warnings.Add(InputBindingWarning.DragAndHoldDrag);
            if (redundant) warnings.Add(InputBindingWarning.RedundantAction);
            return warnings;
        }

        // Redundant means the same action on enabled slots in the same pointer
        // family, regardless of target (e.g. both double-taps select all idle).
        // One-pointer, two-finger and dedicated mouse routes are complementary:
        // Pan on Drag and TwoFingerDrag, or Zoom on Pinch and ScrollWheel, is fine.
        private static int PointerFamily(InputSlot slot)
        {
            if (slot == InputSlot.TwoFingerDrag || slot == InputSlot.Pinch || slot == InputSlot.TwoFingerTap)
                return 1;
            if (slot == InputSlot.MiddleDrag || slot == InputSlot.ScrollWheel) return 2;
            return 0;
        }
    }
}
