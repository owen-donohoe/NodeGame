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
            Pair(0.1f, 4f, 16f); // 2 mm span change is below dead zone.
            Pair(0.2f, 5f, 20f);
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
        public void Golden_ZeroSpanCannotBeginZoom()
        {
            Pair(0f, 1f, 1f);
            Kinds();
            Assert.AreEqual(GestureState.Idle, core.State);
        }
    }
}
