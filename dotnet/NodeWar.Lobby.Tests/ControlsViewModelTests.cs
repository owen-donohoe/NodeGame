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
            Assert.AreEqual(InputBindings.SlotCount, ControlsViewModel.Slots.Count);
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
        public void Groups_AreInTheRequestedOrderWithTheRequestedSlots()
        {
            var titles = new List<string>();
            foreach (ControlsViewModel.ControlsGroup group in ControlsViewModel.Groups) titles.Add(group.Title);
            CollectionAssert.AreEqual(new[] { "Tap", "Drag", "Hold", "Two fingers", "Mouse" }, titles);

            CollectionAssert.AreEqual(new[] { InputSlot.TapVillager, InputSlot.DoubleTapGround, InputSlot.DoubleTapVillager },
                ControlsViewModel.Groups[0].Slots);
            CollectionAssert.AreEqual(new[] { InputSlot.Drag, InputSlot.HoldDrag, InputSlot.DragFromVillager,
                InputSlot.DoubleTapDrag }, ControlsViewModel.Groups[1].Slots);
            CollectionAssert.AreEqual(new[] { InputSlot.Hold }, ControlsViewModel.Groups[2].Slots);
            CollectionAssert.AreEqual(new[] { InputSlot.TwoFingerDrag, InputSlot.Pinch, InputSlot.TwoFingerTap },
                ControlsViewModel.Groups[3].Slots);
            CollectionAssert.AreEqual(new[] { InputSlot.MiddleDrag, InputSlot.ScrollWheel },
                ControlsViewModel.Groups[4].Slots);
            Assert.AreEqual("Camera, layout & hints", ControlsViewModel.CameraAndLayoutTitle);
        }

        [Test]
        public void Rows_FollowGroupOrder()
        {
            ControlsRow[] rows = ControlsViewModel.Rows(Defaults());
            Assert.AreEqual(InputSlot.TapVillager, rows[0].Slot);
            Assert.AreEqual("Tap a villager", rows[0].Label);
            Assert.AreEqual(InputSlot.Drag, rows[3].Slot);
            Assert.AreEqual("Hold + drag", rows[4].Label);
            Assert.AreEqual("Lasso select", rows[4].ActionLabel);
            Assert.AreEqual("Drag from a villager", rows[5].Label);
            Assert.IsFalse(rows[5].Enabled);
            Assert.AreEqual("Double-tap a villager", rows[2].Label);
        }

        [Test]
        public void SingleActionRows_ReportNoDropdown_AndMultiActionRowsListOnlyTheirOwnActions()
        {
            foreach (InputSlot slot in new[] { InputSlot.Pinch, InputSlot.Hold, InputSlot.DoubleTapDrag,
                         InputSlot.DragFromVillager, InputSlot.MiddleDrag, InputSlot.ScrollWheel })
            {
                ControlsRow single = ControlsViewModel.RowFor(Defaults(), slot);
                Assert.IsFalse(single.HasDropdown, slot.ToString());
                Assert.AreEqual(1, single.Options.Count, slot.ToString());
                Assert.IsNotEmpty(single.ActionLabel);
            }

            ControlsRow ground = ControlsViewModel.RowFor(Defaults(), InputSlot.DoubleTapGround);
            Assert.IsTrue(ground.HasDropdown);
            CollectionAssert.AreEqual(new[] { InputAction.ReturnToCore, InputAction.SelectAllIdle,
                InputAction.ToggleFitDefaultZoom }, ground.Options);
            Assert.AreEqual(3, ground.OptionLabels.Count);
            Assert.AreEqual(0, ground.SelectedIndex);
            Assert.AreEqual("Return to core", ground.OptionLabels[ground.SelectedIndex]);
        }

        [Test]
        public void SelectedIndex_FollowsTheChosenAction_AndSetActionOnlyAcceptsAllowedOnes()
        {
            GameSettingsData settings = ControlsViewModel.SetAction(Defaults(), InputSlot.DoubleTapGround,
                InputAction.ToggleFitDefaultZoom);
            ControlsRow row = ControlsViewModel.RowFor(settings, InputSlot.DoubleTapGround);
            Assert.AreEqual(2, row.SelectedIndex);
            Assert.AreEqual(InputAction.ToggleFitDefaultZoom, row.Action);

            GameSettingsData refused = ControlsViewModel.SetAction(settings, InputSlot.DoubleTapGround, InputAction.Order);
            Assert.IsFalse(GameSettingsData.Differ(settings, refused));
            Assert.IsTrue(GameSettingsData.Differ(Defaults(), settings));
            Assert.AreEqual(0, ControlsViewModel.RowFor(Defaults(), InputSlot.DoubleTapGround).SelectedIndex,
                "editing did not touch the original");
        }

        [Test]
        public void Dimmed_FollowsEnabled_AndALockedRowIsNeverDimmed()
        {
            Assert.IsTrue(ControlsViewModel.RowFor(Defaults(), InputSlot.Hold).Dimmed);
            Assert.IsFalse(ControlsViewModel.RowFor(Defaults(), InputSlot.Drag).Dimmed);
            Assert.IsFalse(ControlsViewModel.RowFor(Defaults(), InputSlot.TapVillager).Dimmed);

            GameSettingsData on = ControlsViewModel.ToggleSlot(Defaults(), InputSlot.Hold);
            Assert.IsFalse(ControlsViewModel.RowFor(on, InputSlot.Hold).Dimmed);
            GameSettingsData off = ControlsViewModel.ToggleSlot(Defaults(), InputSlot.Drag);
            Assert.IsTrue(ControlsViewModel.RowFor(off, InputSlot.Drag).Dimmed);
            Assert.IsFalse(ControlsViewModel.RowFor(off, InputSlot.Drag).Locked);
        }

        [Test]
        public void Tooltips_DefaultOn_ToggleClonesAndSurvivesNormalizeAndReset()
        {
            Assert.IsTrue(Defaults().tooltips);
            GameSettingsData off = ControlsViewModel.ToggleTooltips(Defaults());
            Assert.IsFalse(off.tooltips);
            Assert.IsTrue(Defaults().tooltips, "the original is untouched");
            Assert.IsFalse(GameSettingsData.Normalized(off).tooltips);
            Assert.IsTrue(ControlsViewModel.ResetControls(off).tooltips);
        }

        [Test]
        public void DragToZoom_IsDimmedAndInertWhileTheCameraButtonIsOff()
        {
            Assert.IsFalse(ControlsViewModel.CameraZoomDimmed(Defaults()));
            GameSettingsData noButton = ControlsViewModel.ToggleCameraButton(Defaults());
            Assert.IsTrue(ControlsViewModel.CameraZoomDimmed(noButton));
            Assert.IsFalse(GameSettingsData.Differ(noButton, ControlsViewModel.ToggleCameraButtonZoom(noButton)));
            Assert.IsTrue(ControlsViewModel.CameraZoomDimmed(noButton));
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
            Assert.IsTrue(GameSettingsData.Differ(original, ControlsViewModel.ToggleTooltips(original)));
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
            edited = ControlsViewModel.ToggleTooltips(edited);
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
