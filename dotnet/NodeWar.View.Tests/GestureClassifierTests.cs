using System.Collections.Generic;
using NodeWar.Input;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class GestureClassifierTests
    {
        private GestureClassifier core;
        private List<GestureEvent> events;

        [SetUp]
        public void SetUp()
        {
            core = new GestureClassifier(InputBindings.CreateDefault(), 4f, 0.3f, 1.5f, 256, 2.5f, 0.1f);
            events = new List<GestureEvent>();
            core.Published += events.Add;
        }

        private void Primary(float time, PointerPhase phase, float x = 0f, float y = 0f, bool ui = false)
        {
            core.ProcessFrame(new[] { new PointerSample(time, new GesturePoint(x, y), 10, PointerButton.Primary, phase, ui) });
        }

        private void Pair(float time, float ax, float bx, float y = 0f)
        {
            core.ProcessFrame(new[] {
                new PointerSample(time, new GesturePoint(ax, y), 10, PointerButton.Primary, PointerPhase.Held),
                new PointerSample(time, new GesturePoint(ax, y), 10, PointerButton.Touch, PointerPhase.Held),
                new PointerSample(time, new GesturePoint(bx, y), 11, PointerButton.Touch, PointerPhase.Held)
            });
        }

        private void Kinds(params GestureEventKind[] expected)
        {
            CollectionAssert.AreEqual(expected, events.ConvertAll(e => e.Kind));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LiveThresholdUpdatesKeepSavedHoldTimeButUpdateFallback(bool savedSettings)
        {
            if (savedSettings)
            {
                GameSettingsData settings = GameSettingsData.CreateDefault();
                settings.holdTime = 0.6f;
                core.ApplySettings(settings);
            }
            core.UpdateThresholds(4f, 0.8f, 1.5f, 256, 2.5f, 0.1f);
            Primary(0f, PointerPhase.Began);
            Primary(0.6f, PointerPhase.Held);
            Assert.AreEqual(savedSettings ? GestureState.LassoArmed : GestureState.Pending, core.State);
            Primary(0.8f, PointerPhase.Held);
            Assert.IsTrue(core.PanSuppressed);
        }

        [Test]
        public void LiveSlopTuningIsReadOnNextSample()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 3f);
            Assert.AreEqual(GestureState.Pending, core.State);
            core.UpdateThresholds(2f, 0.3f, 1.5f, 256, 2.5f, 0.1f);
            Primary(0.2f, PointerPhase.Held, 3f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void ZeroSpanBecomingValidCanBeginPinchLater()
        {
            Pair(0f, 0f, 0f);
            Pair(0.1f, -5f, 5f);
            Pair(0.2f, -10f, 10f);
            Primary(0.3f, PointerPhase.Ended);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate, GestureEventKind.ZoomEnd);
            Assert.AreEqual(2f, events[1].Scale);
        }

        [Test]
        public void StackedPressEndsPreviousPanAndNewTapCanComplete()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 5f);
            Primary(0.2f, PointerPhase.Began);
            Primary(0.25f, PointerPhase.Ended);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanEnd, GestureEventKind.Cancelled,
                GestureEventKind.PointerDown, GestureEventKind.Tap);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void PrimaryPointerReplacementCannotCompleteAnotherPointersTap()
        {
            Primary(0f, PointerPhase.Began);
            core.ProcessFrame(new[] { new PointerSample(0.1f, new GesturePoint(0f, 0f), 99,
                PointerButton.Primary, PointerPhase.Ended) });
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void CancellationBalancesConcurrentCameraGestures()
        {
            Pair(0f, 0f, 10f);
            Pair(0.1f, 5f, 20f);
            core.Cancel();
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanEnd, GestureEventKind.ZoomEnd, GestureEventKind.Cancelled);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoFingerPanUsesMidpointAndCanRunWithPinch(bool pinch)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Pinch].enabled = pinch;
            core.ApplySettings(settings);
            Pair(0f, 0f, 10f);
            Pair(0.1f, 1f, 11f);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.PanBegin));
            Pair(0.2f, 5f, 20f);
            Primary(0.3f, PointerPhase.Held);
            if (pinch)
                Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate, GestureEventKind.PanBegin,
                    GestureEventKind.PanUpdate, GestureEventKind.PanEnd, GestureEventKind.ZoomEnd);
            else Kinds(GestureEventKind.PanBegin, GestureEventKind.PanUpdate, GestureEventKind.PanEnd);
            GestureEvent begin = events.Find(e => e.Kind == GestureEventKind.PanBegin);
            GestureEvent update = events.Find(e => e.Kind == GestureEventKind.PanUpdate);
            Assert.AreEqual(new GesturePoint(5f, 0f), begin.Position);
            Assert.AreEqual(new GesturePoint(12.5f, 0f), update.Position);
            Assert.AreEqual(GestureState.Idle, core.State);
            Primary(0.4f, PointerPhase.Held, 30f);
            Assert.AreEqual(GestureState.Idle, core.State, "survivor cannot inherit pair stroke");
        }

        [Test]
        public void DisabledTwoFingerDragStillAllowsPinch()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.TwoFingerDrag].enabled = false;
            core.ApplySettings(settings);
            Pair(0f, 0f, 10f);
            Pair(0.1f, 10f, 25f);
            Primary(0.2f, PointerPhase.Ended);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate, GestureEventKind.ZoomEnd);
        }

        [Test]
        public void TwoFingerLassoUsesBoundActionAndEmptyCancelCompletesOnce()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Pinch].enabled = false;
            settings.inputBindings[(int)InputSlot.TwoFingerDrag].action = (int)InputAction.LassoSelect;
            core.ApplySettings(settings);
            Pair(0f, 0f, 10f);
            Pair(0.1f, 5f, 15f);
            Assert.IsTrue(core.PanSuppressed);
            core.Cancel();
            Kinds(GestureEventKind.LassoBegin, GestureEventKind.LassoPoint,
                GestureEventKind.LassoComplete, GestureEventKind.Cancelled);
            Assert.IsEmpty(events[2].Points);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void FingerReplacementEndsPairInsteadOfJumpingCamera()
        {
            Pair(0f, 0f, 10f);
            core.ProcessFrame(new[] {
                new PointerSample(0.1f, new GesturePoint(0f, 0f), 10, PointerButton.Primary, PointerPhase.Held),
                new PointerSample(0.1f, new GesturePoint(0f, 0f), 10, PointerButton.Touch, PointerPhase.Held),
                new PointerSample(0.1f, new GesturePoint(30f, 0f), 12, PointerButton.Touch, PointerPhase.Held)
            });
            Primary(0.2f, PointerPhase.Ended);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomEnd);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void FreshPairOverUiIsBlocked()
        {
            core.ProcessFrame(new[] {
                new PointerSample(0f, new GesturePoint(0f, 0f), 10, PointerButton.Primary, PointerPhase.Began, true),
                new PointerSample(0f, new GesturePoint(0f, 0f), 10, PointerButton.Touch, PointerPhase.Held, true),
                new PointerSample(0f, new GesturePoint(10f, 0f), 11, PointerButton.Touch, PointerPhase.Held)
            });
            Pair(0.1f, 10f, 30f);
            Primary(0.2f, PointerPhase.Ended);
            Kinds();
            Assert.AreEqual(GestureState.Idle, core.State);
        }


        private void Press(float time, PointerPhase phase, GestureTargetClass target, int id = -1, float x = 0f, float y = 0f)
        {
            core.ProcessFrame(new[] { new PointerSample(time, new GesturePoint(x, y), 10, PointerButton.Primary, phase,
                false, target, id) });
        }

        private void TapOn(float time, GestureTargetClass target, int id = -1, float x = 0f)
        {
            Press(time, PointerPhase.Began, target, id, x);
            Press(time + 0.05f, PointerPhase.Ended, target, id, x);
        }

        private void Enable(params InputSlot[] slots)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            foreach (InputSlot slot in slots) settings.inputBindings[(int)slot].enabled = true;
            core.ApplySettings(settings);
        }

        [Test]
        public void Golden_DoubleTapIsOffByDefaultSoBothTapsFire()
        {
            TapOn(0f, GestureTargetClass.None);
            TapOn(0.2f, GestureTargetClass.None);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void DoubleTapGroundFiresFirstTapImmediatelyThenReplacesSecondTap()
        {
            Enable(InputSlot.DoubleTapGround);
            Press(0f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.05f, PointerPhase.Ended, GestureTargetClass.None);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap);
            TapOn(0.25f, GestureTargetClass.None, x: 2f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown,
                GestureEventKind.DoubleTapGround);
            Assert.AreEqual(new GesturePoint(2f, 0f), events[3].Position);
        }

        [Test]
        public void ThirdTapStartsAFreshPairInsteadOfChaining()
        {
            Enable(InputSlot.DoubleTapGround);
            TapOn(0f, GestureTargetClass.None);
            TapOn(0.2f, GestureTargetClass.None);
            TapOn(0.4f, GestureTargetClass.None);
            Assert.AreEqual(GestureEventKind.Tap, events[events.Count - 1].Kind);
            TapOn(0.6f, GestureTargetClass.None);
            Assert.AreEqual(GestureEventKind.DoubleTapGround, events[events.Count - 1].Kind);
        }

        [TestCase(0.45f, 0f)]   // too late: 0.3 s window, measured from release
        [TestCase(0.2f, 9f)]    // too far: 8 mm radius
        [TestCase(-0.5f, 0f)]   // clock went backwards
        public void DoubleTapOutsideWindowOrRadiusIsTwoTaps(float secondTime, float secondX)
        {
            Enable(InputSlot.DoubleTapGround);
            TapOn(0f, GestureTargetClass.None);
            TapOn(secondTime, GestureTargetClass.None, x: secondX);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void DoubleTapOnNodesIsNeverOffered()
        {
            Enable(InputSlot.DoubleTapGround, InputSlot.DoubleTapVillager);
            TapOn(0f, GestureTargetClass.Node, 3);
            TapOn(0.2f, GestureTargetClass.Node, 3);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void DoubleTapMustStayOnTheSameKindOfTarget()
        {
            Enable(InputSlot.DoubleTapGround, InputSlot.DoubleTapVillager);
            TapOn(0f, GestureTargetClass.None);
            TapOn(0.2f, GestureTargetClass.Villager, 4);
            Assert.AreEqual(GestureEventKind.Tap, events[events.Count - 1].Kind);
            events.Clear();
            TapOn(5f, GestureTargetClass.Villager, 4);
            TapOn(5.2f, GestureTargetClass.Villager, 5);
            Assert.AreEqual(GestureEventKind.Tap, events[events.Count - 1].Kind, "different villager");
        }

        [Test]
        public void DoubleTapVillagerRecognisesAVillagerThatTheFirstTapSelected()
        {
            Enable(InputSlot.DoubleTapVillager);
            TapOn(0f, GestureTargetClass.Villager, 4);
            TapOn(0.2f, GestureTargetClass.SelectedVillager, 4);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown,
                GestureEventKind.DoubleTapVillager);
        }

        [Test]
        public void DoubleTapSecondPressThatDragsOrHoldsStaysAnOrdinaryGesture()
        {
            Enable(InputSlot.DoubleTapGround);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, x: 6f);
            Press(0.4f, PointerPhase.Ended, GestureTargetClass.None, x: 6f);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.DoubleTapGround));
            Assert.IsTrue(events.Exists(e => e.Kind == GestureEventKind.PanBegin));
            Assert.AreEqual(GestureState.Idle, core.State);
        }


        private void PairAt(float time, float ax, float bx, float y = 0f, int idB = 11)
        {
            core.ProcessFrame(new[] {
                new PointerSample(time, new GesturePoint(ax, y), 10, PointerButton.Primary, PointerPhase.Held),
                new PointerSample(time, new GesturePoint(ax, y), 10, PointerButton.Touch, PointerPhase.Held),
                new PointerSample(time, new GesturePoint(bx, y), idB, PointerButton.Touch, PointerPhase.Held)
            });
        }

        [Test]
        public void TwoFingerTapIsOffByDefault()
        {
            Pair(0f, 0f, 10f);
            Pair(0.1f, 0f, 10f);
            Primary(0.15f, PointerPhase.Held);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomEnd);
        }

        [Test]
        public void QuickStillTwoFingerTouchIsATapAndSurvivorIsInert()
        {
            Enable(InputSlot.TwoFingerTap);
            Pair(0f, 0f, 10f);
            Pair(0.1f, 1f, 11f);
            Primary(0.2f, PointerPhase.Held);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomEnd, GestureEventKind.TwoFingerTap);
            Primary(0.3f, PointerPhase.Held, 20f);
            Primary(0.4f, PointerPhase.Ended, 20f);
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void TwoFingerTapWorksWithPinchAndDragBothOff()
        {
            Enable(InputSlot.TwoFingerTap);
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.TwoFingerTap].enabled = true;
            settings.inputBindings[(int)InputSlot.Pinch].enabled = false;
            settings.inputBindings[(int)InputSlot.TwoFingerDrag].enabled = false;
            core.ApplySettings(settings);
            Pair(0f, 0f, 10f);
            Primary(0.1f, PointerPhase.Held);
            Kinds(GestureEventKind.TwoFingerTap);
        }

        [Test]
        public void TwoFingerTouchHeldTooLongIsNotATap()
        {
            Enable(InputSlot.TwoFingerTap);
            Pair(0f, 0f, 10f);
            Pair(0.2f, 0f, 10f);
            Primary(0.35f, PointerPhase.Held);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.TwoFingerTap));
        }

        [TestCase(4.1f, 0f, true)]   // midpoint travels past the 4 mm tap slop
        [TestCase(3.9f, 0f, false)]  // wobble inside it
        [TestCase(0f, 6f, true)]     // span grows past the pinch dead zone
        public void TwoFingerTapIsSplitFromDragAndPinchBySlop(float shift, float spread, bool notATap)
        {
            Enable(InputSlot.TwoFingerTap);
            Pair(0f, 0f, 10f);
            Pair(0.1f, shift, 10f + shift + spread);
            Primary(0.2f, PointerPhase.Held);
            Assert.AreEqual(!notATap, events.Exists(e => e.Kind == GestureEventKind.TwoFingerTap));
        }

        [Test]
        public void TwoFingerDragNeverAlsoTaps()
        {
            Enable(InputSlot.TwoFingerTap);
            Pair(0f, 0f, 10f);
            Pair(0.1f, 6f, 16f);
            Pair(0.15f, 0f, 10f); // back where it started: still a drag
            Primary(0.2f, PointerPhase.Held);
            Assert.IsTrue(events.Exists(e => e.Kind == GestureEventKind.PanEnd));
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.TwoFingerTap));
        }

        [Test]
        public void ReplacedFingerNeverTaps()
        {
            Enable(InputSlot.TwoFingerTap);
            Pair(0f, 0f, 10f);
            PairAt(0.1f, 0f, 10f, idB: 12);
            Primary(0.2f, PointerPhase.Held);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.TwoFingerTap));
        }

        public static IEnumerable<int> ToggleMasks()
        {
            for (int mask = 0; mask < 1024; mask++) yield return mask;
        }

        [TestCaseSource(nameof(ToggleMasks))]
        public void EveryTouchToggleCombinationReturnsIdleAndKeepsPlainTaps(int mask)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            // The ten toggleable touch slots are contiguous: HoldDrag through Hold.
            for (int bit = 0; bit < 10; bit++) settings.inputBindings[bit + 1].enabled = (mask & (1 << bit)) != 0;
            var random = new System.Random(2411);
            for (int trace = 0; trace < 12; trace++)
            {
                // Exercise the two single-pointer action choices too.
                settings.inputBindings[(int)InputSlot.Drag].action = (int)(trace % 2 == 0 ? InputAction.Pan : InputAction.LassoSelect);
                settings.inputBindings[(int)InputSlot.HoldDrag].action = (int)(trace % 3 == 0 ? InputAction.Pan : InputAction.LassoSelect);
                core.ApplySettings(settings);
                Primary(0f, PointerPhase.Began, ui: trace == 1);
                for (int sample = 1; sample <= 8; sample++)
                {
                    float x = (float)(random.NextDouble() * 12 - 6);
                    float y = (float)(random.NextDouble() * 12 - 6);
                    if (trace % 3 == 0 && sample >= 4) Pair(sample * 0.08f, x, x + 10f, y);
                    else Primary(sample * 0.08f, PointerPhase.Held, x, y);
                }
                Primary(0.8f, trace % 4 == 0 ? PointerPhase.Cancelled : PointerPhase.Ended);
                Primary(0.9f, PointerPhase.Ended);
                Assert.AreEqual(GestureState.Idle, core.State, "trace " + trace);
                Assert.IsFalse(core.PanSuppressed);
                int beforeTap = events.Count;
                Primary(1f, PointerPhase.Began);
                Primary(1.1f, PointerPhase.Ended);
                Assert.AreEqual(beforeTap + 2, events.Count, "locked tap grammar");
                Assert.AreEqual(GestureEventKind.PointerDown, events[beforeTap].Kind);
                Assert.AreEqual(GestureEventKind.Tap, events[beforeTap + 1].Kind);
                Assert.AreEqual(GestureState.Idle, core.State);
                events.Clear();
            }
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held);
            Primary(0.4f, PointerPhase.Held, 6f);
            Primary(0.5f, PointerPhase.Held, 6f, 6f);
            Primary(0.6f, PointerPhase.Held, 0f, 6f);
            Primary(0.7f, PointerPhase.Ended);
            Assert.AreEqual(GestureState.Idle, core.State, "stationary hold then polygon");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HoldDragBoundToPanBeginsAtTimer(bool dragEnabled)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Drag].enabled = dragEnabled;
            settings.inputBindings[(int)InputSlot.HoldDrag].action = (int)InputAction.Pan;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(0.2f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Pending, core.State);
            Primary(0.3f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Panning, core.State);
            Assert.IsFalse(core.PanSuppressed);
            Primary(0.4f, PointerPhase.Held, 6f);
            Primary(0.5f, PointerPhase.Ended, 6f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanUpdate, GestureEventKind.PanEnd);
        }

        [Test]
        public void OnlyHoldDragOnEarlyMovementDoesNothing()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Drag].enabled = false;
            settings.inputBindings[(int)InputSlot.HoldDrag].action = (int)InputAction.Pan;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 5f);
            Primary(0.3f, PointerPhase.Held);
            Primary(0.4f, PointerPhase.Ended);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled);
        }

        [TestCase(false, 0.1f)]
        [TestCase(false, 0.5f)]
        [TestCase(true, 0.1f)]
        [TestCase(true, 0.5f)]
        public void DragBoundToLassoStartsAtSlopWithoutTimer(bool holdEnabled, float time)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.HoldDrag].enabled = holdEnabled;
            settings.inputBindings[(int)InputSlot.Drag].action = (int)InputAction.LassoSelect;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(time, PointerPhase.Held, 5f);
            Assert.AreEqual(GestureState.Lassoing, core.State);
            Assert.IsTrue(core.PanSuppressed);
            Primary(time + 0.1f, PointerPhase.Ended, 5f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.LassoBegin,
                GestureEventKind.LassoPoint, GestureEventKind.LassoComplete);
            CollectionAssert.AreEqual(new[] { new GesturePoint(0f, 0f), new GesturePoint(5f, 0f) }, events[4].Points);
        }

        [Test]
        public void DisabledDragDoesNotPanAndReturningWithinSlopDoesNotTap()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Drag].enabled = false;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 6f);
            Primary(0.2f, PointerPhase.Ended);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void DisabledHoldDoesNotArmAndDragWorksAfterTimer()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.HoldDrag].enabled = false;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(0.4f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Pending, core.State);
            Primary(0.5f, PointerPhase.Held, 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void DisabledPinchConsumesPairWithoutZoomAndDoesNotResumeSurvivor()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Pinch].enabled = false;
            settings.inputBindings[(int)InputSlot.TwoFingerDrag].enabled = false;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Pair(0.1f, 0f, 10f);
            Pair(0.2f, 0f, 20f);
            Primary(0.3f, PointerPhase.Held, 8f);
            Primary(0.4f, PointerPhase.Ended, 8f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [TestCase(InputSlot.MiddleDrag)]
        [TestCase(InputSlot.ScrollWheel)]
        public void SettingsExposeDisabledMouseSlotToCamera(InputSlot slot)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)slot].enabled = false;
            core.ApplySettings(settings);
            Assert.IsFalse(core.IsEnabled(slot));
        }

        [Test]
        public void SettingsChangeEndsActivePanBeforeCancelling()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 6f);
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Drag].enabled = false;
            core.ApplySettings(settings);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanEnd, GestureEventKind.Cancelled);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void SettingsHoldTimeOverridesSerializedFallback()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.holdTime = 0.6f;
            core.ApplySettings(settings);
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Pending, core.State);
            Primary(0.6f, PointerPhase.Held);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
        }

        [Test]
        public void MissingSettingsUseProvidedFallbackHoldTime()
        {
            core = new GestureClassifier(InputBindings.CreateDefault(), 4f, 0.7f, 1.5f, 256, 2.5f, 0.1f);
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Pending, core.State);
            Primary(0.7f, PointerPhase.Held);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
        }

        [TestCase(0, 0.3f)]
        [TestCase(4, 0.3f)]
        [TestCase(5, 0.15f)]
        public void SettingsHoldTimeUsesMigrationAndClamp(int version, float expected)
        {
            core.ApplySettings(new GameSettingsData { version = version });
            Primary(0f, PointerPhase.Began);
            Primary(expected, PointerPhase.Held);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
        }

        [Test]
        public void Golden_TapUsesDownPositionAndOnlyCommitsOnRelease()
        {
            Primary(0f, PointerPhase.Began, 2f);
            Kinds(GestureEventKind.PointerDown);
            Primary(0.1f, PointerPhase.Ended, 3f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap);
            Assert.AreEqual(new GesturePoint(2f, 0f), events[1].Position);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void Golden_PanCancelsThenBeginsAtDownAndUpdatesAtCurrentPosition()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 5f);
            Primary(0.2f, PointerPhase.Held, 7f);
            Primary(0.25f, PointerPhase.Ended, 8f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanUpdate, GestureEventKind.PanEnd);
            Assert.AreEqual(new GesturePoint(0f, 0f), events[2].Position);
            Assert.AreEqual(new GesturePoint(5f, 0f), events[3].Position);
            Assert.AreEqual(new GesturePoint(7f, 0f), events[4].Position);
        }

        [Test]
        public void Golden_HoldArmsAtTimerAndDecimatesLasso()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
            Assert.IsTrue(core.PanSuppressed);
            Primary(0.4f, PointerPhase.Held, 1f);
            Primary(0.5f, PointerPhase.Held, 2f);
            Primary(0.6f, PointerPhase.Ended, 9f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.LassoBegin,
                GestureEventKind.LassoPoint, GestureEventKind.LassoComplete);
            CollectionAssert.AreEqual(new[] { new GesturePoint(0f, 0f), new GesturePoint(2f, 0f) }, events[4].Points);
        }

        [Test]
        public void IntentionalChange_DeadZoneHitchNowPans()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.31f, PointerPhase.Held, 5f);
            Assert.AreEqual(GestureState.Panning, core.State);
            Primary(0.4f, PointerPhase.Ended, 5f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanEnd);
        }

        [Test]
        public void SlowDriftWithinTapSlopPansAtTimer()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 0.8f);
            Primary(0.2f, PointerPhase.Held, 1.6f);
            Primary(0.3f, PointerPhase.Held, 3f);
            Assert.AreEqual(GestureState.Panning, core.State);
            Assert.IsFalse(core.PanSuppressed);
        }

        [Test]
        public void StillnessCountsPathEvenWhenPointerReturnsToStart()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 1f);
            Primary(0.3f, PointerPhase.Held);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void ExactlyAtStillnessLimitCanArmHold()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held, 1.5f);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
        }

        [Test]
        public void Golden_ReleaseAtTimerWithoutHeldFrameIsNotTap()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Ended);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled);
        }

        [Test]
        public void Golden_ExactlyAtSlopRemainsTap()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 4f);
            Primary(0.2f, PointerPhase.Ended, 4f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void Golden_UiStrokeIsLatchedEvenAfterLeavingUi()
        {
            Primary(0f, PointerPhase.Began, ui: true);
            Primary(0.1f, PointerPhase.Held, 8f);
            Primary(0.2f, PointerPhase.Ended, 8f);
            Kinds();
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void Golden_RightClickIsIndependentAndUiGuarded()
        {
            Primary(0f, PointerPhase.Began);
            core.ProcessFrame(new[] { new PointerSample(0.1f, new GesturePoint(7f, 8f), 0,
                PointerButton.Secondary, PointerPhase.Began) });
            core.ProcessFrame(new[] { new PointerSample(0.2f, new GesturePoint(7f, 8f), 0,
                PointerButton.Secondary, PointerPhase.Began, true) });
            Kinds(GestureEventKind.PointerDown, GestureEventKind.SecondaryClick);
            Assert.AreEqual(new GesturePoint(7f, 8f), events[1].Position);
            Assert.AreEqual(GestureState.Pending, core.State);
        }

        [Test]
        public void Golden_MiddleDragAndScrollPublishNothingInPointerSource()
        {
            foreach (PointerPhase phase in new[] { PointerPhase.Began, PointerPhase.Held, PointerPhase.Ended })
                core.ProcessFrame(new[] { new PointerSample(0f, new GesturePoint(5f, 0f), 0, PointerButton.Middle, phase) });
            core.ProcessFrame(new[] { new PointerSample(0f, new GesturePoint(0f, 0f), 0, PointerButton.Scroll, PointerPhase.Held) });
            Kinds(); // CameraController reads these directly, outside the source.
        }

        [Test]
        public void Golden_PinchUsesStartSpanAndDoesNotPan()
        {
            Pair(0f, 0f, 10f);
            Pair(0.1f, -1f, 11f); // 2 mm span change is below dead zone.
            Pair(0.2f, -2.5f, 12.5f);
            Pair(0.3f, 0f, 10f);
            Primary(0.4f, PointerPhase.Held);
            Primary(0.5f, PointerPhase.Ended);
            Kinds(GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate, GestureEventKind.ZoomEnd);
            Assert.AreEqual(1.5f, events[1].Scale);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void Golden_SecondFingerEndsPanBeforePinch()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.1f, PointerPhase.Held, 5f);
            Pair(0.2f, 5f, 15f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.PanBegin,
                GestureEventKind.PanUpdate, GestureEventKind.PanEnd, GestureEventKind.Cancelled, GestureEventKind.ZoomBegin);
        }

        [Test]
        public void Golden_SecondFingerAbandonsLassoAndBlocksUntilRelease()
        {
            Primary(0f, PointerPhase.Began);
            Primary(0.3f, PointerPhase.Held);
            Pair(0.4f, 0f, 10f);
            Pair(0.5f, 0f, 20f);
            Primary(0.6f, PointerPhase.Ended);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.LassoBegin,
                GestureEventKind.Cancelled, GestureEventKind.LassoComplete);
            Assert.IsEmpty(events[4].Points);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void ZeroSpanCannotBeginZoomButCanTrackTwoFingerDrag()
        {
            Pair(0f, 1f, 1f);
            Kinds();
            Assert.AreEqual(GestureState.TwoFinger, core.State);
            Primary(0.1f, PointerPhase.Ended);
            Assert.AreEqual(GestureState.Idle, core.State);
        }
    }
}
