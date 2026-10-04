using System;
using System.Collections.Generic;
using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class ControlsViewModelTests
    {
        private static GameSettingsData Defaults()
        {
            return GameSettingsData.CreateDefault();
        }

        [Test]
        public void Rows_CoverEverySlotOnce()
        {
            ControlsRow[] rows = ControlsViewModel.Rows(Defaults());
            Assert.AreEqual(InputBindings.SlotCount, rows.Length);
            var seen = new bool[InputBindings.SlotCount];
            foreach (ControlsRow row in rows)
            {
                Assert.IsFalse(seen[(int)row.Slot], row.Label);
                seen[(int)row.Slot] = true;
                Assert.IsNotEmpty(row.Label);
                Assert.IsNotEmpty(row.ActionLabel);
            }
        }

        [Test]
        public void Rows_FollowSpecOrderAtTheTop()
        {
            ControlsRow[] rows = ControlsViewModel.Rows(Defaults());
            Assert.AreEqual(InputSlot.HoldDrag, rows[0].Slot);
            Assert.AreEqual("Hold + drag", rows[0].Label);
            Assert.AreEqual("Lasso select", rows[0].ActionLabel);
            Assert.IsTrue(rows[0].Enabled);
            Assert.AreEqual("Drag from villager", rows[4].Label);
            Assert.IsFalse(rows[4].Enabled);
        }

        [Test]
        public void SingleActionRows_DoNotCycle_MultiActionRowsDo()
        {
            Assert.IsFalse(ControlsViewModel.RowFor(Defaults(), InputSlot.Pinch).ActionCycles);
            Assert.IsFalse(ControlsViewModel.RowFor(Defaults(), InputSlot.Hold).ActionCycles);
            Assert.IsTrue(ControlsViewModel.RowFor(Defaults(), InputSlot.Drag).ActionCycles);
            Assert.IsTrue(ControlsViewModel.RowFor(Defaults(), InputSlot.DoubleTapGround).ActionCycles);
        }

        [Test]
        public void LockedRow_ShowsAlways_AndStillCycles()
        {
            ControlsRow row = ControlsViewModel.RowFor(Defaults(), InputSlot.TapVillager);
            Assert.IsTrue(row.Locked);
            Assert.AreEqual("always", row.StateText);
            Assert.IsTrue(row.Enabled);
            Assert.IsTrue(row.ActionCycles);
            Assert.AreEqual("Add / remove", row.ActionLabel);

            GameSettingsData cycled = ControlsViewModel.CycleAction(Defaults(), InputSlot.TapVillager);
            Assert.AreEqual("Replace", ControlsViewModel.RowFor(cycled, InputSlot.TapVillager).ActionLabel);
            cycled = ControlsViewModel.CycleAction(cycled, InputSlot.TapVillager);
            Assert.AreEqual("Add / remove", ControlsViewModel.RowFor(cycled, InputSlot.TapVillager).ActionLabel);
        }

        [Test]
        public void LockedRow_CannotBeSwitchedOff()
        {
            GameSettingsData result = ControlsViewModel.ToggleSlot(Defaults(), InputSlot.TapVillager);
            Assert.IsTrue(result.inputBindings[(int)InputSlot.TapVillager].enabled);
            Assert.IsFalse(GameSettingsData.Differ(Defaults(), result));

            // Even a save that stored it off opens on.
            GameSettingsData corrupt = Defaults();
            corrupt.inputBindings = (InputBinding[])corrupt.inputBindings.Clone();
            corrupt.inputBindings[(int)InputSlot.TapVillager].enabled = false;
            Assert.IsTrue(ControlsViewModel.RowFor(corrupt, InputSlot.TapVillager).Enabled);
        }

        [Test]
        public void ToggleSlot_FlipsOnlyThatSlot()
        {
            GameSettingsData result = ControlsViewModel.ToggleSlot(Defaults(), InputSlot.Hold);
            Assert.IsTrue(result.inputBindings[(int)InputSlot.Hold].enabled);
            for (int i = 0; i < InputBindings.SlotCount; i++)
                if (i != (int)InputSlot.Hold)
                    Assert.AreEqual(Defaults().inputBindings[i], result.inputBindings[i], "slot " + i);
            result = ControlsViewModel.ToggleSlot(result, InputSlot.Hold);
            Assert.IsFalse(GameSettingsData.Differ(Defaults(), result));
        }

        [Test]
        public void Cycle_VisitsOnlyAllowedActions_AndWraps()
        {
            foreach (InputSlot slot in ControlsViewModel.Slots)
            {
                GameSettingsData settings = Defaults();
                var visited = new HashSet<InputAction>();
                for (int i = 0; i < 6; i++)
                {
                    settings = ControlsViewModel.CycleAction(settings, slot);
                    InputAction action = (InputAction)settings.inputBindings[(int)slot].action;
                    Assert.IsTrue(InputBindings.IsAllowed(slot, action), slot + " -> " + action);
                    visited.Add(action);
                }
                Assert.AreEqual(InputBindings.DefinitionFor(slot).AllowedActions.Count, visited.Count, slot.ToString());
            }
        }

        [Test]
        public void Cycle_SkipsActionsThatAreNotAllowed()
        {
            // Drag allows Pan and Lasso only; Zoom and Order exist but are skipped.
            Assert.AreEqual(InputAction.LassoSelect, ControlsViewModel.NextAction(InputSlot.Drag, InputAction.Pan));
            Assert.AreEqual(InputAction.Pan, ControlsViewModel.NextAction(InputSlot.Drag, InputAction.LassoSelect));
            // Three-way slot walks the allowed list in order.
            Assert.AreEqual(InputAction.SelectAllIdle, ControlsViewModel.NextAction(InputSlot.DoubleTapGround, InputAction.ReturnToCore));
            Assert.AreEqual(InputAction.ToggleFitDefaultZoom, ControlsViewModel.NextAction(InputSlot.DoubleTapGround, InputAction.SelectAllIdle));
            Assert.AreEqual(InputAction.ReturnToCore, ControlsViewModel.NextAction(InputSlot.DoubleTapGround, InputAction.ToggleFitDefaultZoom));
            // A disallowed stored action falls to the slot default, not a neighbour.
            Assert.AreEqual(InputAction.Pan, ControlsViewModel.NextAction(InputSlot.Drag, InputAction.Zoom));
            Assert.AreEqual(InputAction.Zoom, ControlsViewModel.NextAction(InputSlot.Pinch, InputAction.Order));
            // A single-action slot stays put.
            Assert.AreEqual(InputAction.Zoom, ControlsViewModel.NextAction(InputSlot.Pinch, InputAction.Zoom));
        }

        [Test]
        public void Edits_DoNotMutateTheInput()
        {
            GameSettingsData original = Defaults();
            ControlsViewModel.ToggleSlot(original, InputSlot.Hold);
            ControlsViewModel.CycleAction(original, InputSlot.Drag);
            ControlsViewModel.ResetControls(original);
            Assert.IsFalse(GameSettingsData.Differ(Defaults(), original));
        }

        [Test]
        public void EditingProducesAValueDifferReportsAsChanged()
        {
            GameSettingsData original = Defaults();
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.ToggleSlot(original, InputSlot.Hold)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.CycleAction(original, InputSlot.Drag)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.CycleAction(original, InputSlot.TapVillager)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.ToggleCameraButton(original)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.ToggleCameraButtonZoom(original)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.ToggleSelectionBar(original)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.CycleControlsSide(original)));
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.SetHoldTime(original, 0.5f)));
        }

        [Test]
        public void EditingAnAliasedCopy_StillDiffers()
        {
            // The page's Capture starts from `current`, so the edited value shares
            // the array with the saved one unless the edit clones it.
            GameSettingsData saved = Defaults();
            GameSettingsData captured = saved;
            captured = ControlsViewModel.ToggleSlot(captured, InputSlot.DoubleTapGround);
            Assert.IsTrue(GameSettingsData.Differ(saved, captured));
            Assert.IsFalse(saved.inputBindings[(int)InputSlot.DoubleTapGround].enabled);
        }

        [Test]
        public void ResetControls_RestoresCreateDefault()
        {
            GameSettingsData edited = Defaults();
            edited = ControlsViewModel.ToggleSlot(edited, InputSlot.Hold);
            edited = ControlsViewModel.CycleAction(edited, InputSlot.TapVillager);
            edited = ControlsViewModel.ToggleCameraButton(edited);
            edited = ControlsViewModel.ToggleCameraButtonZoom(edited);
            edited = ControlsViewModel.ToggleSelectionBar(edited);
            edited = ControlsViewModel.CycleControlsSide(edited);
            edited = ControlsViewModel.SetHoldTime(edited, 0.9f);
            Assert.IsTrue(GameSettingsData.Differ(GameSettingsData.CreateDefault(), edited));

            GameSettingsData reset = ControlsViewModel.ResetControls(edited);
            Assert.IsFalse(GameSettingsData.Differ(GameSettingsData.CreateDefault(), reset));
        }

        [Test]
        public void ResetControls_LeavesNonControlSettingsAlone()
        {
            GameSettingsData edited = Defaults();
            edited.masterVolume = 0.1f;
            edited.reducedMotion = true;
            edited.frameCap = 2;
            edited = ControlsViewModel.ToggleCameraButton(edited);

            GameSettingsData reset = ControlsViewModel.ResetControls(edited);
            Assert.AreEqual(0.1f, reset.masterVolume);
            Assert.IsTrue(reset.reducedMotion);
            Assert.AreEqual(2, reset.frameCap);
            Assert.IsTrue(reset.showCameraButton);
        }

        [Test]
        public void ResetControls_IsADetachedCopyOfTheDefaults()
        {
            GameSettingsData reset = ControlsViewModel.ResetControls(Defaults());
            reset.inputBindings[(int)InputSlot.Hold].enabled = true;
            Assert.IsFalse(GameSettingsData.CreateDefault().inputBindings[(int)InputSlot.Hold].enabled);
        }

        [Test]
        public void HoldTime_IsClampedAndShownWithTwoDecimals()
        {
            Assert.AreEqual(GameSettingsData.MinHoldTime, ControlsViewModel.SetHoldTime(Defaults(), 0f).holdTime);
            Assert.AreEqual(GameSettingsData.MaxHoldTime, ControlsViewModel.SetHoldTime(Defaults(), 5f).holdTime);
            Assert.AreEqual("0.30 s", ControlsViewModel.HoldTimeLabel(GameSettingsData.DefaultHoldTime));
            Assert.AreEqual("1.00 s", ControlsViewModel.HoldTimeLabel(1f));
            Assert.AreEqual("0.15 s", ControlsViewModel.HoldTimeLabel(0.15f));
        }

        [Test]
        public void ControlsSide_CyclesBetweenRightAndLeft()
        {
            GameSettingsData settings = Defaults();
            Assert.AreEqual("Right", ControlsViewModel.SideLabel(settings.controlsSide));
            settings = ControlsViewModel.CycleControlsSide(settings);
            Assert.AreEqual(1, settings.controlsSide);
            Assert.AreEqual("Left", ControlsViewModel.SideLabel(settings.controlsSide));
            settings = ControlsViewModel.CycleControlsSide(settings);
            Assert.AreEqual(0, settings.controlsSide);
        }

        [Test]
        public void Warnings_DefaultsWarnOnceAndNothingZoomsIsListed()
        {
            // The shipped defaults carry exactly one warning: Drag and Hold + drag together.
            Assert.AreEqual(1, InputBindings.CheckWarnings(Defaults()).Count);
            StringAssert.Contains("told apart", ControlsViewModel.WarningsText(Defaults()));
            Assert.AreEqual(string.Empty, ControlsViewModel.WarningsText(ControlsViewModel.ToggleSlot(Defaults(), InputSlot.HoldDrag)));

            GameSettingsData settings = Defaults();
            settings = ControlsViewModel.ToggleSlot(settings, InputSlot.Pinch);
            settings = ControlsViewModel.ToggleSlot(settings, InputSlot.ScrollWheel);
            settings = ControlsViewModel.ToggleCameraButtonZoom(settings);
            StringAssert.Contains("Nothing zooms", ControlsViewModel.WarningsText(settings));
        }

        [Test]
        public void Warnings_OneLinePerWarning_MatchingTheCheck()
        {
            // Everything that pans or zooms off: both NoPan and NoZoom fire.
            GameSettingsData settings = Defaults();
            foreach (InputSlot slot in new[] { InputSlot.HoldDrag, InputSlot.Drag, InputSlot.TwoFingerDrag,
                         InputSlot.MiddleDrag, InputSlot.Pinch, InputSlot.ScrollWheel })
                settings = ControlsViewModel.ToggleSlot(settings, slot);
            settings = ControlsViewModel.ToggleCameraButtonZoom(settings);

            int expected = InputBindings.CheckWarnings(settings).Count;
            Assert.AreEqual(2, expected);
            string[] lines = ControlsViewModel.WarningsText(settings).Split('\n');
            Assert.AreEqual(expected, lines.Length);
            foreach (string line in lines) StringAssert.StartsWith("⚠", line);
        }

        [Test]
        public void EveryActionSlotAndWarningHasALabel()
        {
            foreach (InputSlot slot in Enum.GetValues(typeof(InputSlot)))
                Assert.IsNotEmpty(ControlsViewModel.SlotLabel(slot));
            foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
                Assert.IsNotEmpty(ControlsViewModel.ActionLabel(action));
            foreach (InputBindingWarning warning in Enum.GetValues(typeof(InputBindingWarning)))
                Assert.AreNotEqual(warning.ToString(), ControlsViewModel.WarningText(warning));
        }
    }
}
