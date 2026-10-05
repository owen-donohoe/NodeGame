using System;
using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class InputBindingsTests
    {
        [TestCase(InputSlot.TapVillager, 0, true, InputAction.AddRemove, true, new[] { InputAction.AddRemove, InputAction.Replace })]
        [TestCase(InputSlot.HoldDrag, 1, true, InputAction.LassoSelect, false, new[] { InputAction.LassoSelect, InputAction.Pan })]
        [TestCase(InputSlot.Drag, 2, true, InputAction.Pan, false, new[] { InputAction.Pan, InputAction.LassoSelect })]
        [TestCase(InputSlot.DragFromVillager, 3, false, InputAction.Order, false, new[] { InputAction.Order })]
        [TestCase(InputSlot.TwoFingerDrag, 4, true, InputAction.Pan, false, new[] { InputAction.Pan, InputAction.LassoSelect })]
        [TestCase(InputSlot.Pinch, 5, true, InputAction.Zoom, false, new[] { InputAction.Zoom })]
        [TestCase(InputSlot.DoubleTapGround, 6, false, InputAction.ReturnToCore, false,
            new[] { InputAction.ReturnToCore, InputAction.SelectAllIdle, InputAction.ToggleFitDefaultZoom })]
        [TestCase(InputSlot.DoubleTapVillager, 7, false, InputAction.SelectAllIdle, false,
            new[] { InputAction.SelectAllIdle, InputAction.SelectAllOnNode })]
        [TestCase(InputSlot.TwoFingerTap, 8, false, InputAction.ClearSelection, false,
            new[] { InputAction.ClearSelection, InputAction.ReturnToCore })]
        [TestCase(InputSlot.DoubleTapDrag, 9, false, InputAction.Zoom, false, new[] { InputAction.Zoom })]
        [TestCase(InputSlot.Hold, 10, false, InputAction.OpenInfo, false, new[] { InputAction.OpenInfo })]
        [TestCase(InputSlot.MiddleDrag, 11, true, InputAction.Pan, false, new[] { InputAction.Pan })]
        [TestCase(InputSlot.ScrollWheel, 12, true, InputAction.Zoom, false, new[] { InputAction.Zoom })]
        public void Table_MatchesSpec(InputSlot slot, int index, bool enabled, InputAction action,
            bool locked, InputAction[] allowed)
        {
            Assert.AreEqual(index, (int)slot);
            InputSlotDefinition definition = InputBindings.DefinitionFor(slot);
            Assert.AreEqual(enabled, definition.DefaultEnabled);
            Assert.AreEqual(action, definition.DefaultAction);
            Assert.AreEqual(locked, definition.Locked);
            CollectionAssert.AreEqual(allowed, definition.AllowedActions);
            Assert.AreEqual(new InputBinding(enabled, action), InputBindings.DefaultFor(slot));
            Assert.AreEqual(InputBindings.DefaultFor(slot), GameSettingsData.CreateDefault().inputBindings[index]);

            foreach (InputAction candidate in Enum.GetValues(typeof(InputAction)))
                Assert.AreEqual(Array.IndexOf(allowed, candidate) >= 0, InputBindings.IsAllowed(slot, candidate));
            Assert.IsFalse(InputBindings.IsAllowed(slot, (InputAction)(-1)));
            Assert.IsFalse(InputBindings.IsAllowed(slot, (InputAction)999));
        }

        [Test]
        public void Table_CoversEverySlotAndAction()
        {
            Assert.AreEqual(13, Enum.GetValues(typeof(InputSlot)).Length);
            Assert.AreEqual(13, InputBindings.SlotCount);
            Assert.AreEqual(12, Enum.GetValues(typeof(InputAction)).Length);
            foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
            {
                bool found = false;
                for (int i = 0; i < InputBindings.SlotCount; i++)
                    found |= InputBindings.IsAllowed((InputSlot)i, action);
                Assert.IsTrue(found, action.ToString());
            }
            Assert.IsFalse(InputBindings.IsAllowed((InputSlot)(-1), InputAction.Pan));
            Assert.IsFalse(InputBindings.IsAllowed((InputSlot)13, InputAction.Pan));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void OlderSaves_GetAllControlDefaults(int version)
        {
            var stored = new GameSettingsData { version = version, masterVolume = 0.17f };
            GameSettingsData result = GameSettingsData.Normalized(stored);
            CollectionAssert.AreEqual(InputBindings.CreateDefault(), result.inputBindings);
            Assert.AreEqual(0.3f, result.holdTime);
            Assert.IsTrue(result.showCameraButton);
            Assert.IsTrue(result.cameraButtonZoom);
            Assert.AreEqual(0, result.controlsSide);
            Assert.IsTrue(result.showSelectionBar);
            Assert.IsTrue(result.tooltips);
            Assert.AreEqual(0, result.cameraButtonTarget);
            if (version > 0) Assert.AreEqual(0.17f, result.masterVolume);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(12)]
        [TestCase(13)]
        public void Normalized_PadsShortArraysAndPreservesChoices(int length)
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.inputBindings = new InputBinding[length];
            for (int i = 0; i < length; i++)
                stored.inputBindings[i] = new InputBinding(false, InputBindings.DefinitionFor((InputSlot)i).DefaultAction);
            if (length > 0) stored.inputBindings[0].action = (int)InputAction.Replace;

            GameSettingsData result = GameSettingsData.Normalized(stored);
            Assert.AreEqual(InputBindings.SlotCount, result.inputBindings.Length);
            for (int i = 0; i < InputBindings.SlotCount; i++)
                Assert.AreEqual(i < length
                    ? new InputBinding(i == 0, i == 0 ? InputAction.Replace : InputBindings.DefinitionFor((InputSlot)i).DefaultAction)
                    : InputBindings.DefaultFor((InputSlot)i), result.inputBindings[i], "slot " + i);
        }

        [Test]
        public void Normalized_MissingArrayGetsDefaults()
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.inputBindings = null;
            CollectionAssert.AreEqual(InputBindings.CreateDefault(), GameSettingsData.Normalized(stored).inputBindings);
        }

        [TestCase(-1)]
        [TestCase(999)]
        [TestCase((int)InputAction.OpenInfo)]
        public void Normalized_InvalidActionFallsBackWithoutEnablingSlot(int action)
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.inputBindings[(int)InputSlot.Drag] = new InputBinding { enabled = false, action = action };
            InputBinding result = GameSettingsData.Normalized(stored).inputBindings[(int)InputSlot.Drag];
            Assert.AreEqual((int)InputAction.Pan, result.action);
            Assert.IsFalse(result.enabled);
        }

        [Test]
        public void Normalized_LockedSlotEnabledButActionStillEditable()
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.inputBindings[0] = new InputBinding(false, InputAction.Replace);
            Assert.AreEqual(new InputBinding(true, InputAction.Replace), GameSettingsData.Normalized(stored).inputBindings[0]);
            Assert.IsFalse(stored.inputBindings[0].enabled, "normalization must not mutate the source array");
        }

        [Test]
        public void Normalized_TruncatesExtraSlotsAndIsIdempotent()
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            Array.Resize(ref stored.inputBindings, 20);
            GameSettingsData normalized = GameSettingsData.Normalized(stored);
            Assert.AreEqual(13, normalized.inputBindings.Length);
            Assert.IsFalse(GameSettingsData.Differ(normalized, GameSettingsData.Normalized(normalized)));
        }

        [TestCase(-1f, 0.15f)]
        [TestCase(0f, 0.15f)]
        [TestCase(0.15f, 0.15f)]
        [TestCase(0.3f, 0.3f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        [TestCase(float.NaN, 0.3f)]
        [TestCase(float.NegativeInfinity, 0.15f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Normalized_ClampsHoldTime(float stored, float expected)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.holdTime = stored;
            Assert.AreEqual(expected, GameSettingsData.Normalized(settings).holdTime);
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 0)]
        public void Normalized_ControlsSideFallsBackToRight(int stored, int expected)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.controlsSide = stored;
            Assert.AreEqual(expected, GameSettingsData.Normalized(settings).controlsSide);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 0)]
        [TestCase(-1, 0)]
        public void CameraButtonTarget_NormalizesToCoreOrBoard(int stored, int expected)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.cameraButtonTarget = stored;
            Assert.AreEqual(expected, GameSettingsData.Normalized(settings).cameraButtonTarget);
        }

        [Test]
        public void CameraButtonTarget_DefaultsToCore_AndDifferSeesIt()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();
            Assert.AreEqual(0, defaults.cameraButtonTarget);
            GameSettingsData board = defaults;
            board.cameraButtonTarget = 1;
            Assert.IsTrue(GameSettingsData.Differ(defaults, board));
            Assert.AreEqual(1, GameSettingsData.Normalized(board).cameraButtonTarget);
        }

        [Test]
        public void Normalized_CurrentVersionKeepsControlsOff()
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.showSelectionBar = false;
            settings.tooltips = false;
            GameSettingsData result = GameSettingsData.Normalized(settings);
            Assert.IsFalse(result.showCameraButton);
            Assert.IsFalse(result.cameraButtonZoom);
            Assert.IsFalse(result.showSelectionBar);
            Assert.IsFalse(result.tooltips);
            for (int i = 1; i < result.inputBindings.Length; i++) Assert.IsFalse(result.inputBindings[i].enabled);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Differ_DetectsSingleBindingEdit(bool changeEnabled)
        {
            GameSettingsData a = GameSettingsData.CreateDefault();
            GameSettingsData b = GameSettingsData.CreateDefault();
            if (changeEnabled) b.inputBindings[(int)InputSlot.Drag].enabled = false;
            else b.inputBindings[(int)InputSlot.Drag].action = (int)InputAction.LassoSelect;
            Assert.IsTrue(GameSettingsData.Differ(a, b));
            Assert.IsTrue(GameSettingsData.Differ(b, a));
        }

        [Test]
        public void Differ_ComparesNullAndLength()
        {
            Assert.IsFalse(InputBindings.Differ(null, null));
            Assert.IsTrue(InputBindings.Differ(null, InputBindings.CreateDefault()));
            Assert.IsTrue(InputBindings.Differ(InputBindings.CreateDefault(), null));
            Assert.IsTrue(InputBindings.Differ(new InputBinding[0], InputBindings.CreateDefault()));
        }

        [Test]
        public void Warnings_DefaultsOnlyWarnAboutTiming()
        {
            CollectionAssert.AreEqual(new[] { InputBindingWarning.DragAndHoldDrag },
                InputBindings.CheckWarnings(GameSettingsData.CreateDefault()));
        }

        [Test]
        public void Warnings_NoCameraInputsWarnAboutPanAndZoom()
        {
            CollectionAssert.AreEqual(new[] { InputBindingWarning.NoPan, InputBindingWarning.NoZoom },
                InputBindings.CheckWarnings(AllOptionalInputsOff()));
        }

        [TestCase(InputSlot.HoldDrag)]
        [TestCase(InputSlot.Drag)]
        [TestCase(InputSlot.TwoFingerDrag)]
        [TestCase(InputSlot.MiddleDrag)]
        public void Warnings_AnyPanBindingPreventsNoPan(InputSlot slot)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)slot] = new InputBinding(true, InputAction.Pan);
            CollectionAssert.DoesNotContain(InputBindings.CheckWarnings(settings), InputBindingWarning.NoPan);
            settings.inputBindings[(int)slot].enabled = false;
            CollectionAssert.Contains(InputBindings.CheckWarnings(settings), InputBindingWarning.NoPan);
        }

        [TestCase(InputSlot.Pinch)]
        [TestCase(InputSlot.DoubleTapDrag)]
        [TestCase(InputSlot.ScrollWheel)]
        public void Warnings_AnyZoomBindingPreventsNoZoom(InputSlot slot)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)slot].enabled = true;
            CollectionAssert.DoesNotContain(InputBindings.CheckWarnings(settings), InputBindingWarning.NoZoom);
            settings.inputBindings[(int)slot].enabled = false;
            CollectionAssert.Contains(InputBindings.CheckWarnings(settings), InputBindingWarning.NoZoom);
        }

        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void Warnings_CameraZoomRequiresVisibleButtonAndZoomEnabled(bool visible, bool zoom, bool warns)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.showCameraButton = visible;
            settings.cameraButtonZoom = zoom;
            Assert.AreEqual(warns, InputBindings.CheckWarnings(settings).Contains(InputBindingWarning.NoZoom));
            CollectionAssert.Contains(InputBindings.CheckWarnings(settings), InputBindingWarning.NoPan);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Warnings_TimingRequiresBothDragSlots(bool drag, bool holdDrag)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)InputSlot.Drag].enabled = drag;
            settings.inputBindings[(int)InputSlot.HoldDrag].enabled = holdDrag;
            Assert.AreEqual(drag && holdDrag,
                InputBindings.CheckWarnings(settings).Contains(InputBindingWarning.DragAndHoldDrag));
        }

        [TestCase(InputSlot.HoldDrag, InputSlot.Drag, InputAction.Pan)]
        [TestCase(InputSlot.HoldDrag, InputSlot.Drag, InputAction.LassoSelect)]
        [TestCase(InputSlot.DoubleTapGround, InputSlot.DoubleTapVillager, InputAction.SelectAllIdle)]
        public void Warnings_SameFamilyDuplicatesWarnOnlyWhenBothEnabled(InputSlot a, InputSlot b, InputAction action)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)a] = new InputBinding(true, action);
            settings.inputBindings[(int)b] = new InputBinding(true, action);
            CollectionAssert.Contains(InputBindings.CheckWarnings(settings), InputBindingWarning.RedundantAction);
            settings.inputBindings[(int)a].enabled = false;
            CollectionAssert.DoesNotContain(InputBindings.CheckWarnings(settings), InputBindingWarning.RedundantAction);
        }

        [TestCase(InputSlot.Drag, InputSlot.TwoFingerDrag, InputAction.Pan)]
        [TestCase(InputSlot.Drag, InputSlot.MiddleDrag, InputAction.Pan)]
        [TestCase(InputSlot.Pinch, InputSlot.ScrollWheel, InputAction.Zoom)]
        [TestCase(InputSlot.Pinch, InputSlot.DoubleTapDrag, InputAction.Zoom)]
        [TestCase(InputSlot.DoubleTapGround, InputSlot.TwoFingerTap, InputAction.ReturnToCore)]
        public void Warnings_ComplementaryFamiliesDoNotWarn(InputSlot a, InputSlot b, InputAction action)
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)a] = new InputBinding(true, action);
            settings.inputBindings[(int)b] = new InputBinding(true, action);
            CollectionAssert.DoesNotContain(InputBindings.CheckWarnings(settings), InputBindingWarning.RedundantAction);
        }

        [Test]
        public void Warnings_EmitEachWarningOnceInSpecOrder()
        {
            GameSettingsData settings = AllOptionalInputsOff();
            settings.inputBindings[(int)InputSlot.Drag] = new InputBinding(true, InputAction.LassoSelect);
            settings.inputBindings[(int)InputSlot.HoldDrag] = new InputBinding(true, InputAction.LassoSelect);
            CollectionAssert.AreEqual(new[] { InputBindingWarning.NoPan, InputBindingWarning.NoZoom,
                InputBindingWarning.DragAndHoldDrag, InputBindingWarning.RedundantAction }, InputBindings.CheckWarnings(settings));
        }

        private static GameSettingsData AllOptionalInputsOff()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            for (int i = 1; i < settings.inputBindings.Length; i++) settings.inputBindings[i].enabled = false;
            settings.showCameraButton = false;
            settings.cameraButtonZoom = false;
            return settings;
        }
    }
}
