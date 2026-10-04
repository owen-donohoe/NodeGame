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


        private void ZoomScales(out float[] scales)
        {
            scales = events.FindAll(e => e.Kind == GestureEventKind.ZoomUpdate).ConvertAll(e => e.Scale).ToArray();
        }

        [Test]
        public void DoubleTapDragIsOffByDefaultAndSecondPressPans()
        {
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, y: 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void DoubleTapDragZoomsFromTheSlopCrossingAndDownIsIn()
        {
            Enable(InputSlot.DoubleTapDrag);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.25f, PointerPhase.Held, GestureTargetClass.None, y: -2f);
            Assert.AreEqual(GestureState.Pending, core.State);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, y: -5f);
            Assert.AreEqual(GestureState.OneHandZoom, core.State);
            Assert.IsFalse(core.PanSuppressed);
            Press(0.4f, PointerPhase.Held, GestureTargetClass.None, y: -30f);
            Press(0.5f, PointerPhase.Held, GestureTargetClass.None, y: 20f);
            Press(0.6f, PointerPhase.Ended, GestureTargetClass.None, y: 20f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap, GestureEventKind.PointerDown,
                GestureEventKind.Cancelled, GestureEventKind.ZoomBegin, GestureEventKind.ZoomUpdate,
                GestureEventKind.ZoomUpdate, GestureEventKind.ZoomEnd);
            ZoomScales(out float[] scales);
            Assert.AreEqual(2f, scales[0], 1e-5f, "25 mm below the slop crossing doubles");
            Assert.AreEqual(0.5f, scales[1], 1e-5f, "25 mm above it halves");
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void DoubleTapDragNeverFiresTheDoubleTapActionOnRelease()
        {
            Enable(InputSlot.DoubleTapDrag, InputSlot.DoubleTapGround);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, y: -6f);
            Press(0.4f, PointerPhase.Ended, GestureTargetClass.None, y: -6f);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.DoubleTapGround));
        }

        [Test]
        public void DoubleTapWithoutMovementStillFiresOnReleaseWhenDragIsAlsoOn()
        {
            Enable(InputSlot.DoubleTapDrag, InputSlot.DoubleTapGround);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.25f, PointerPhase.Held, GestureTargetClass.None, y: 1f);
            Assert.IsFalse(events.Exists(e => e.Kind == GestureEventKind.DoubleTapGround), "not before release");
            Press(0.3f, PointerPhase.Ended, GestureTargetClass.None, y: 1f);
            Assert.AreEqual(GestureEventKind.DoubleTapGround, events[events.Count - 1].Kind);
        }

        [Test]
        public void DoubleTapDragNeedsAPrecedingTapAndNodesDoNotCount()
        {
            Enable(InputSlot.DoubleTapDrag);
            Press(0f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.None, y: 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
            Press(0.2f, PointerPhase.Ended, GestureTargetClass.None, y: 6f);
            TapOn(1f, GestureTargetClass.Node, 3);
            Press(1.2f, PointerPhase.Began, GestureTargetClass.Node, 3);
            Press(1.3f, PointerPhase.Held, GestureTargetClass.Node, 3, y: 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void SecondFingerDuringOneHandedZoomEndsItBalanced()
        {
            Enable(InputSlot.DoubleTapDrag);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, y: -6f);
            Pair(0.4f, 0f, 10f);
            Primary(0.5f, PointerPhase.Ended);
            Assert.AreEqual(1, events.FindAll(e => e.Kind == GestureEventKind.ZoomBegin).Count);
            Assert.AreEqual(1, events.FindAll(e => e.Kind == GestureEventKind.ZoomEnd).Count);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void SettingsChangeDuringOneHandedZoomEndsIt()
        {
            Enable(InputSlot.DoubleTapDrag);
            TapOn(0f, GestureTargetClass.None);
            Press(0.2f, PointerPhase.Began, GestureTargetClass.None);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.None, y: -6f);
            core.ApplySettings(GameSettingsData.CreateDefault());
            Assert.AreEqual(GestureEventKind.Cancelled, events[events.Count - 1].Kind);
            Assert.AreEqual(1, events.FindAll(e => e.Kind == GestureEventKind.ZoomEnd).Count);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        private void HoldOn(GestureTargetClass target, int id, float releaseAt, params (float t, float x)[] moves)
        {
            Press(0f, PointerPhase.Began, target, id);
            foreach (var move in moves) Press(move.t, PointerPhase.Held, target, id, move.x);
            Press(releaseAt, PointerPhase.Ended, target, id);
        }

        private int Count(GestureEventKind kind) => events.FindAll(e => e.Kind == kind).Count;

        private void HoldOnlyInfo()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Hold].enabled = true;
            settings.inputBindings[(int)InputSlot.HoldDrag].enabled = false;
            core.ApplySettings(settings);
        }

        [Test]
        public void Golden_HoldInfoIsOffByDefault()
        {
            HoldOn(GestureTargetClass.Node, 3, 0.6f, (0.3f, 0f));
            Assert.AreEqual(0, Count(GestureEventKind.HoldInfo));
        }

        [TestCase(GestureTargetClass.Node)]
        [TestCase(GestureTargetClass.Villager)]
        [TestCase(GestureTargetClass.SelectedVillager)]
        public void HoldInfoFiresAtTheTimerWhenNothingElseClaimsTheHold(GestureTargetClass target)
        {
            HoldOnlyInfo();
            Press(0f, PointerPhase.Began, target, 3);
            Press(0.29f, PointerPhase.Held, target, 3);
            Assert.AreEqual(0, Count(GestureEventKind.HoldInfo));
            Press(0.3f, PointerPhase.Held, target, 3);
            Assert.AreEqual(GestureState.HoldFired, core.State);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.HoldInfo);
            Press(0.5f, PointerPhase.Held, target, 3, 30f);
            Press(0.6f, PointerPhase.Ended, target, 3, 30f);
            Assert.AreEqual(3, events.Count, "drag after the hold is inert");
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void HoldInfoIgnoresGround()
        {
            HoldOnlyInfo();
            HoldOn(GestureTargetClass.None, -1, 0.6f, (0.3f, 0f));
            Assert.AreEqual(0, Count(GestureEventKind.HoldInfo));
        }

        [Test]
        public void HoldInfoNeedsStillness()
        {
            HoldOnlyInfo();
            HoldOn(GestureTargetClass.Node, 3, 0.5f, (0.1f, 1f), (0.3f, 0f));
            Assert.AreEqual(0, Count(GestureEventKind.HoldInfo));
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void HoldInfoOnReleaseWhenHoldDragArmedALassoThatNeverMoved()
        {
            Enable(InputSlot.Hold);
            HoldOn(GestureTargetClass.Villager, 3, 0.6f, (0.3f, 0f));
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.LassoBegin,
                GestureEventKind.LassoComplete, GestureEventKind.HoldInfo);
        }

        [Test]
        public void HoldInfoOnReleaseWhenHoldDragIsPanAndNeverMoved()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.Hold].enabled = true;
            settings.inputBindings[(int)InputSlot.HoldDrag].action = (int)InputAction.Pan;
            core.ApplySettings(settings);
            HoldOn(GestureTargetClass.Node, 3, 0.6f, (0.3f, 0f));
            Assert.AreEqual(GestureEventKind.HoldInfo, events[events.Count - 1].Kind);
            Assert.AreEqual(GestureEventKind.PanEnd, events[events.Count - 2].Kind);
        }

        [Test]
        public void HoldThenDragIsALassoNotInfo()
        {
            Enable(InputSlot.Hold);
            HoldOn(GestureTargetClass.Villager, 3, 0.8f, (0.3f, 0f), (0.4f, 5f), (0.5f, 10f));
            Assert.AreEqual(0, Count(GestureEventKind.HoldInfo));
            Assert.AreEqual(1, Count(GestureEventKind.LassoComplete));
        }

        [Test]
        public void ReleaseBeforeHoldTimeIsATapNotAHold()
        {
            Enable(InputSlot.Hold);
            HoldOn(GestureTargetClass.Node, 3, 0.2f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void SecondFingerAfterHoldFiredIsBlockedCleanly()
        {
            HoldOnlyInfo();
            Press(0f, PointerPhase.Began, GestureTargetClass.Node, 3);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.Node, 3);
            Pair(0.4f, 0f, 10f);
            Primary(0.5f, PointerPhase.Ended);
            Assert.AreEqual(1, Count(GestureEventKind.HoldInfo));
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void Golden_DragFromSelectedVillagerPansWhenOrderDragIsOff()
        {
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void OrderDragFromSelectedVillagerBeginsAtSlopAndEndsWithPosition()
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.05f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 3f);
            Assert.AreEqual(GestureState.Pending, core.State);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 6f);
            Assert.AreEqual(GestureState.Ordering, core.State);
            Assert.IsFalse(core.PanSuppressed);
            Press(0.2f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 20f);
            Press(0.3f, PointerPhase.Ended, GestureTargetClass.SelectedVillager, 3, 25f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Cancelled, GestureEventKind.OrderBegin,
                GestureEventKind.OrderUpdate, GestureEventKind.OrderUpdate, GestureEventKind.OrderEnd);
            Assert.AreEqual(new GesturePoint(0f, 0f), events[2].Position);
            Assert.AreEqual(new GesturePoint(25f, 0f), events[5].Position);
            Assert.AreEqual(GestureState.Idle, core.State);
        }

        [Test]
        public void UnselectedVillagerDragNeverOrdersOrSelects()
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, GestureTargetClass.Villager, 3);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.Villager, 3, 6f);
            Press(0.2f, PointerPhase.Ended, GestureTargetClass.Villager, 3, 12f);
            Assert.AreEqual(0, Count(GestureEventKind.OrderBegin));
            Assert.AreEqual(0, Count(GestureEventKind.Tap));
            Assert.AreEqual(1, Count(GestureEventKind.PanBegin));
        }

        [TestCase(GestureTargetClass.None)]
        [TestCase(GestureTargetClass.Node)]
        public void OrderDragStartsOnlyFromVillagers(GestureTargetClass target)
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, target, 3);
            Press(0.1f, PointerPhase.Held, target, 3, 6f);
            Assert.AreEqual(GestureState.Panning, core.State);
        }

        [Test]
        public void OrderDragWithDragOffDoesNotNeedPanAndStillOrders()
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inputBindings[(int)InputSlot.DragFromVillager].enabled = true;
            settings.inputBindings[(int)InputSlot.Drag].enabled = false;
            core.ApplySettings(settings);
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 6f);
            Assert.AreEqual(GestureState.Ordering, core.State);
        }

        [Test]
        public void SelectedVillagerWithinSlopStaysATap()
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 3f);
            Press(0.15f, PointerPhase.Ended, GestureTargetClass.SelectedVillager, 3, 3f);
            Kinds(GestureEventKind.PointerDown, GestureEventKind.Tap);
        }

        [Test]
        public void OrderDragAbortedBySecondFingerOrSettingsEndsCancelled()
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 6f);
            Pair(0.2f, 0f, 10f);
            Primary(0.3f, PointerPhase.Ended);
            Assert.AreEqual(1, Count(GestureEventKind.OrderCancel));
            Assert.AreEqual(0, Count(GestureEventKind.OrderEnd));
            Assert.AreEqual(GestureState.Idle, core.State);
            events.Clear();
            Press(5f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(5.1f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3, 6f);
            core.ApplySettings(GameSettingsData.CreateDefault());
            Assert.AreEqual(1, Count(GestureEventKind.OrderCancel));
        }

        [Test]
        public void HoldStillOnSelectedVillagerStillArmsTheLassoNotTheOrder()
        {
            Enable(InputSlot.DragFromVillager);
            Press(0f, PointerPhase.Began, GestureTargetClass.SelectedVillager, 3);
            Press(0.3f, PointerPhase.Held, GestureTargetClass.SelectedVillager, 3);
            Assert.AreEqual(GestureState.LassoArmed, core.State);
        }

        public static IEnumerable<int> ToggleMasks()
        {
            for (int mask = 0; mask < 1024; mask++) yield return mask;
        }

        private void PrimaryOn(float time, PointerPhase phase, GestureTargetClass target, int id,
            float x = 0f, float y = 0f, bool ui = false)
        {
            core.ProcessFrame(new[] { new PointerSample(time, new GesturePoint(x, y), 10, PointerButton.Primary, phase,
                ui, target, id) });
        }

        private static readonly GestureTargetClass[] SweepTargets =
            { GestureTargetClass.None, GestureTargetClass.Villager, GestureTargetClass.SelectedVillager, GestureTargetClass.Node };

        // Every Begin has exactly one End or Cancel, whatever interrupted it.
        private void AssertBalanced(string label)
        {
            Assert.AreEqual(Count(GestureEventKind.PanBegin), Count(GestureEventKind.PanEnd), label + " pan");
            Assert.AreEqual(Count(GestureEventKind.ZoomBegin), Count(GestureEventKind.ZoomEnd), label + " zoom");
            Assert.AreEqual(Count(GestureEventKind.LassoBegin), Count(GestureEventKind.LassoComplete), label + " lasso");
            Assert.AreEqual(Count(GestureEventKind.OrderBegin),
                Count(GestureEventKind.OrderEnd) + Count(GestureEventKind.OrderCancel), label + " order");
        }

        [Test]
        public void NewSlotsAllDefaultOff()
        {
            foreach (InputSlot slot in new[] { InputSlot.DragFromVillager, InputSlot.DoubleTapGround,
                InputSlot.DoubleTapVillager, InputSlot.TwoFingerTap, InputSlot.DoubleTapDrag, InputSlot.Hold })
                Assert.IsFalse(InputBindings.DefaultFor(slot).enabled, slot.ToString());
        }

        [Test]
        public void Golden_DefaultsPublishNoNewGestureOnAnyTarget()
        {
            foreach (GestureTargetClass target in SweepTargets)
            {
                float t = events.Count * 10f + 100f;
                PrimaryOn(t, PointerPhase.Began, target, 3);
                PrimaryOn(t + 0.05f, PointerPhase.Ended, target, 3);
                PrimaryOn(t + 0.2f, PointerPhase.Began, target, 3);
                PrimaryOn(t + 0.25f, PointerPhase.Ended, target, 3);
                PrimaryOn(t + 1f, PointerPhase.Began, target, 3);
                PrimaryOn(t + 1.4f, PointerPhase.Held, target, 3);
                PrimaryOn(t + 1.8f, PointerPhase.Ended, target, 3);
                PrimaryOn(t + 3f, PointerPhase.Began, target, 3);
                PrimaryOn(t + 3.1f, PointerPhase.Held, target, 3, 8f);
                PrimaryOn(t + 3.2f, PointerPhase.Ended, target, 3, 8f);
                Pair(t + 5f, 0f, 10f);
                Primary(t + 5.1f, PointerPhase.Ended);
            }
            GestureEventKind[] added = { GestureEventKind.DoubleTapGround, GestureEventKind.DoubleTapVillager,
                GestureEventKind.TwoFingerTap, GestureEventKind.HoldInfo, GestureEventKind.OrderBegin,
                GestureEventKind.OrderUpdate, GestureEventKind.OrderEnd, GestureEventKind.OrderCancel };
            foreach (GestureEventKind kind in added) Assert.AreEqual(0, Count(kind), kind.ToString());
            Assert.AreEqual(8, Count(GestureEventKind.Tap), "every tap fires, two per target");
            AssertBalanced("defaults");
        }

        [TestCaseSource(nameof(ToggleMasks))]
        public void EveryTouchToggleCombinationReturnsIdleAndKeepsPlainTaps(int mask)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            // The ten toggleable touch slots are contiguous: HoldDrag through Hold, which
            // takes in every new slot (order drag, both double-taps, two-finger tap,
            // double-tap + drag, hold).
            for (int bit = 0; bit < 10; bit++) settings.inputBindings[bit + 1].enabled = (mask & (1 << bit)) != 0;
            var random = new System.Random(2411);
            for (int trace = 0; trace < 12; trace++)
            {
                // Exercise the single-pointer action choices and every target class.
                settings.inputBindings[(int)InputSlot.Drag].action = (int)(trace % 2 == 0 ? InputAction.Pan : InputAction.LassoSelect);
                settings.inputBindings[(int)InputSlot.HoldDrag].action = (int)(trace % 3 == 0 ? InputAction.Pan : InputAction.LassoSelect);
                core.ApplySettings(settings);
                GestureTargetClass target = SweepTargets[trace % 4];
                float t0 = trace * 10f;
                if (trace % 3 == 1)
                {
                    // A tap followed by a second press that taps, wobbles or drags.
                    TapOn(t0, target, 3);
                    PrimaryOn(t0 + 0.15f, PointerPhase.Began, target, 3);
                    if (trace % 2 == 0) PrimaryOn(t0 + 0.2f, PointerPhase.Held, target, 3, 0f, -(float)(random.NextDouble() * 20));
                    PrimaryOn(t0 + 0.25f, PointerPhase.Ended, target, 3);
                }
                if (trace % 4 == 2)
                {
                    // A quick two-finger touch.
                    Pair(t0 + 0.5f, 0f, 10f);
                    Pair(t0 + 0.55f, 0f, 10f);
                    Primary(t0 + 0.6f, PointerPhase.Held);
                    Primary(t0 + 0.65f, PointerPhase.Ended);
                }
                float b = t0 + 1f;
                PrimaryOn(b, PointerPhase.Began, target, 3, ui: trace == 1);
                for (int sample = 1; sample <= 8; sample++)
                {
                    float x = (float)(random.NextDouble() * 12 - 6);
                    float y = (float)(random.NextDouble() * 12 - 6);
                    if (trace % 3 == 0 && sample >= 4) Pair(b + sample * 0.08f, x, x + 10f, y);
                    else PrimaryOn(b + sample * 0.08f, PointerPhase.Held, target, 3, x, y);
                }
                PrimaryOn(b + 0.8f, trace % 4 == 0 ? PointerPhase.Cancelled : PointerPhase.Ended, target, 3);
                PrimaryOn(b + 0.9f, PointerPhase.Ended, target, 3);
                Assert.AreEqual(GestureState.Idle, core.State, "trace " + trace);
                Assert.IsFalse(core.PanSuppressed);
                AssertBalanced("trace " + trace);
                events.Clear();
                int beforeTap = events.Count;
                TapOn(t0 + 5f, GestureTargetClass.None);
                Assert.AreEqual(beforeTap + 2, events.Count, "locked tap grammar");
                Assert.AreEqual(GestureEventKind.PointerDown, events[beforeTap].Kind);
                Assert.AreEqual(GestureEventKind.Tap, events[beforeTap + 1].Kind);
                Assert.AreEqual(GestureState.Idle, core.State);
                events.Clear();
            }
            PrimaryOn(200f, PointerPhase.Began, GestureTargetClass.Node, 3);
            PrimaryOn(200.3f, PointerPhase.Held, GestureTargetClass.Node, 3);
            PrimaryOn(200.4f, PointerPhase.Held, GestureTargetClass.Node, 3, 6f);
            PrimaryOn(200.5f, PointerPhase.Held, GestureTargetClass.Node, 3, 6f, 6f);
            PrimaryOn(200.6f, PointerPhase.Held, GestureTargetClass.Node, 3, 0f, 6f);
            PrimaryOn(200.7f, PointerPhase.Ended, GestureTargetClass.Node, 3);
            Assert.AreEqual(GestureState.Idle, core.State, "stationary hold then polygon");
            AssertBalanced("polygon");
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
