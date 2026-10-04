using System;
using System.Collections.Generic;
using NodeWar.Lobby;

namespace NodeWar.Input
{
    public enum GestureState { Idle, Pending, Panning, LassoArmed, Lassoing, Blocked, Pinching, Cancelled, TwoFinger }
    public enum PointerButton { Primary, Touch, Secondary, Middle, Scroll }
    public enum PointerPhase { None, Began, Held, Ended, Cancelled }
    public enum GestureEventKind
    {
        PointerDown, Cancelled, Tap, SecondaryClick, PanBegin, PanUpdate, PanEnd,
        LassoBegin, LassoPoint, LassoComplete, ZoomBegin, ZoomUpdate, ZoomEnd,
        DoubleTapGround, DoubleTapVillager
    }

    /// <summary>What the adapter found under a press. Resolved once, on touch-down.</summary>
    public enum GestureTargetClass { None, Villager, SelectedVillager, Node }

    public readonly struct GesturePoint
    {
        public readonly float X;
        public readonly float Y;
        public GesturePoint(float x, float y) { X = x; Y = y; }
        public static float Distance(GesturePoint a, GesturePoint b)
        {
            float x = a.X - b.X, y = a.Y - b.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }
    }

    /// <summary>One frame's pointer reading, in seconds and millimetres. UI blocking is resolved by the adapter.</summary>
    public readonly struct PointerSample
    {
        public readonly float Time;
        public readonly GesturePoint Position;
        public readonly int PointerId;
        public readonly PointerButton Button;
        public readonly PointerPhase Phase;
        public readonly bool OverUI;
        /// <summary>Read on a primary Began sample only.</summary>
        public readonly GestureTargetClass Target;
        public readonly int TargetId;
        public PointerSample(float time, GesturePoint position, int pointerId, PointerButton button,
            PointerPhase phase, bool overUI = false, GestureTargetClass target = GestureTargetClass.None,
            int targetId = -1)
        {
            Time = time; Position = position; PointerId = pointerId;
            Button = button; Phase = phase; OverUI = overUI;
            Target = target; TargetId = targetId;
        }
    }

    public readonly struct GestureEvent
    {
        public readonly GestureEventKind Kind;
        public readonly GesturePoint Position;
        public readonly float Scale;
        public readonly IReadOnlyList<GesturePoint> Points;
        public GestureEvent(GestureEventKind kind, GesturePoint position, float scale, IReadOnlyList<GesturePoint> points)
        {
            Kind = kind; Position = position; Scale = scale; Points = points;
        }
    }

    /// <summary>
    /// Pointer classification with no device, scene or clock of its own.
    /// ProcessFrame receives the primary pointer plus the active touch snapshot;
    /// batching matters because pinch takes priority over a primary transition.
    /// </summary>
    public sealed class GestureClassifier
    {
        public const float DefaultHoldStillnessMm = 1.5f;
        private InputBinding[] bindings;
        private float tapSlop;
        private float holdTime;
        private bool hasSettings;
        private float decimation;
        private int maxPoints;
        private float pinchDeadZone;
        private float minPinchSpan;
        private GesturePoint downPos;
        private float downTime;
        private GesturePoint previousPos;
        private float pathLength;
        private float holdStillness;
        private bool tapExceededSlop;
        private float pinchStartSpan;
        private GesturePoint pairDown;
        private int pairIdA;
        private int pairIdB;
        private int primaryId;
        private bool pairZoom;
        private bool pairPan;
        private bool pairLasso;
        private GestureTargetClass downClass;
        private int downId;
        private bool doubleCandidate;
        private bool lastTapValid;
        private float lastTapTime;
        private GesturePoint lastTapPos;
        private GestureTargetClass lastTapClass;
        private int lastTapId;
        private readonly List<GesturePoint> points = new List<GesturePoint>();

        /// <summary>Longest gap between the first tap's release and the second press.</summary>
        public float DoubleTapTime { get; set; } = 0.3f;
        /// <summary>How far the second press may land from the first tap, in mm.</summary>
        public float DoubleTapRadiusMm { get; set; } = 8f;

        public GestureState State { get; private set; }
        public bool PanSuppressed => State == GestureState.LassoArmed || State == GestureState.Lassoing || pairLasso;
        private bool PairActive => State == GestureState.Pinching || State == GestureState.TwoFinger;
        public IReadOnlyList<GesturePoint> CurrentStroke => points;
        public event Action<GestureEvent> Published;

        public GestureClassifier(InputBinding[] bindings, float tapSlopMm, float holdTime,
            float decimationMm, int maxPoints, float pinchDeadZoneMm, float minPinchSpanMm,
            float holdStillnessMm = DefaultHoldStillnessMm)
        {
            Configure(bindings, tapSlopMm, holdTime, decimationMm, maxPoints, pinchDeadZoneMm, minPinchSpanMm, holdStillnessMm);
        }

        public void Configure(InputBinding[] bindings, float tapSlopMm, float holdTime,
            float decimationMm, int maxPoints, float pinchDeadZoneMm, float minPinchSpanMm,
            float holdStillnessMm = DefaultHoldStillnessMm)
        {
            this.bindings = InputBindings.Normalized(bindings);
            UpdateThresholds(tapSlopMm, holdTime, decimationMm, maxPoints, pinchDeadZoneMm, minPinchSpanMm, holdStillnessMm);
        }

        public void UpdateThresholds(float tapSlopMm, float fallbackHoldTime, float decimationMm,
            int maxPoints, float pinchDeadZoneMm, float minPinchSpanMm,
            float holdStillnessMm = DefaultHoldStillnessMm)
        {
            tapSlop = tapSlopMm;
            if (!hasSettings) holdTime = fallbackHoldTime;
            decimation = decimationMm;
            this.maxPoints = maxPoints;
            pinchDeadZone = pinchDeadZoneMm;
            minPinchSpan = minPinchSpanMm;
            holdStillness = holdStillnessMm;
        }

        private void Emit(GestureEventKind kind, GesturePoint position = default, float scale = 0f)
        {
            Published?.Invoke(new GestureEvent(kind, position, scale,
                kind == GestureEventKind.LassoComplete ? points.ToArray() : null));
        }

        public void ApplySettings(GameSettingsData settings)
        {
            settings = GameSettingsData.Normalized(settings);
            if (InputBindings.Differ(bindings, settings.inputBindings) || holdTime != settings.holdTime)
                Cancel();
            bindings = settings.inputBindings;
            holdTime = settings.holdTime;
            hasSettings = true;
        }

        public bool IsEnabled(InputSlot slot) => bindings[(int)slot].enabled;
        public InputAction ActionFor(InputSlot slot) => (InputAction)bindings[(int)slot].action;

        public void ProcessFrame(IReadOnlyList<PointerSample> samples)
        {
            bool hasPrimary = false;
            PointerSample primary = default;
            int touches = 0;
            GesturePoint a = default, b = default;
            int idA = 0, idB = 0;
            bool pairOverUI = false;
            for (int i = 0; i < samples.Count; i++)
            {
                PointerSample sample = samples[i];
                if (sample.Button == PointerButton.Secondary && sample.Phase == PointerPhase.Began && !sample.OverUI)
                    Emit(GestureEventKind.SecondaryClick, sample.Position);
                if (sample.Button == PointerButton.Primary) { primary = sample; hasPrimary = true; }
                if (sample.Button != PointerButton.Touch ||
                    (sample.Phase != PointerPhase.Began && sample.Phase != PointerPhase.Held)) continue;
                if (touches == 0) { a = sample.Position; idA = sample.PointerId; pairOverUI = sample.OverUI; }
                if (touches == 1) { b = sample.Position; idB = sample.PointerId; pairOverUI |= sample.OverUI; }
                touches++;
            }

            if (!hasPrimary) return;
            if (touches >= 2)
            {
                float span = GesturePoint.Distance(a, b);
                GesturePoint midpoint = new GesturePoint((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
                if (PairActive)
                {
                    // A replaced finger must not inherit the departing finger's anchors.
                    if (!((idA == pairIdA && idB == pairIdB) || (idA == pairIdB && idB == pairIdA)))
                    {
                        EndPair();
                        State = GestureState.Blocked;
                        return;
                    }
                    if (!pairZoom && IsEnabled(InputSlot.Pinch) && span > minPinchSpan)
                    {
                        pairZoom = true;
                        State = GestureState.Pinching;
                        pinchStartSpan = span;
                        Emit(GestureEventKind.ZoomBegin);
                    }
                    else if (pairZoom && span > minPinchSpan && Math.Abs(span - pinchStartSpan) >= pinchDeadZone)
                        Emit(GestureEventKind.ZoomUpdate, scale: span / pinchStartSpan);
                    if (pairPan) Emit(GestureEventKind.PanUpdate, midpoint);
                    else if (pairLasso)
                    {
                        if (Append(midpoint)) Emit(GestureEventKind.LassoPoint, midpoint);
                    }
                    else if (IsEnabled(InputSlot.TwoFingerDrag) && GesturePoint.Distance(midpoint, pairDown) > tapSlop)
                    {
                        if (ActionFor(InputSlot.TwoFingerDrag) == InputAction.Pan)
                        {
                            pairPan = true;
                            Emit(GestureEventKind.PanBegin, pairDown);
                            Emit(GestureEventKind.PanUpdate, midpoint);
                        }
                        else
                        {
                            pairLasso = true;
                            points.Clear();
                            Append(pairDown);
                            Emit(GestureEventKind.LassoBegin, pairDown);
                            if (Append(midpoint)) Emit(GestureEventKind.LassoPoint, midpoint);
                        }
                    }
                    return;
                }
                if (State == GestureState.Blocked) return;
                if (PanSuppressed)
                {
                    Cancel();
                    State = GestureState.Blocked;
                    return;
                }
                if (pairOverUI || (!IsEnabled(InputSlot.Pinch) && !IsEnabled(InputSlot.TwoFingerDrag)))
                {
                    Cancel();
                    State = GestureState.Blocked;
                    return;
                }
                if (span <= minPinchSpan && !IsEnabled(InputSlot.TwoFingerDrag)) return;
                if (State == GestureState.Panning) Emit(GestureEventKind.PanEnd);
                if (State != GestureState.Idle) Emit(GestureEventKind.Cancelled);
                pairZoom = IsEnabled(InputSlot.Pinch) && span > minPinchSpan;
                State = pairZoom ? GestureState.Pinching : GestureState.TwoFinger;
                pinchStartSpan = span;
                pairDown = midpoint;
                pairIdA = idA;
                pairIdB = idB;
                pairPan = pairLasso = false;
                points.Clear();
                if (pairZoom) Emit(GestureEventKind.ZoomBegin);
                return;
            }
            if (PairActive)
            {
                EndPair();
                return;
            }

            if (State != GestureState.Idle && State != GestureState.Blocked && primary.PointerId != primaryId)
            {
                Cancel();
                State = primary.Phase == PointerPhase.Ended ? GestureState.Idle : GestureState.Blocked;
                return;
            }

            switch (primary.Phase)
            {
                case PointerPhase.Began:
                    if (State != GestureState.Idle) Cancel();
                    downPos = primary.Position;
                    downTime = primary.Time;
                    primaryId = primary.PointerId;
                    previousPos = downPos;
                    pathLength = 0f;
                    tapExceededSlop = false;
                    points.Clear();
                    downClass = primary.Target;
                    downId = primary.TargetId;
                    doubleCandidate = !primary.OverUI && MatchesLastTap(primary);
                    State = primary.OverUI ? GestureState.Blocked : GestureState.Pending;
                    if (!primary.OverUI) Emit(GestureEventKind.PointerDown, downPos);
                    break;
                case PointerPhase.Held: Continue(primary); break;
                case PointerPhase.Ended: End(primary); break;
                case PointerPhase.Cancelled: Cancel(); break;
            }
        }

        private void Continue(PointerSample sample)
        {
            switch (State)
            {
                case GestureState.Pending:
                    pathLength += GesturePoint.Distance(sample.Position, previousPos);
                    previousPos = sample.Position;
                    float moved = GesturePoint.Distance(sample.Position, downPos);
                    tapExceededSlop |= moved > tapSlop;
                    float held = sample.Time - downTime;
                    // A hitch cannot strand a drag between the slop and timer.
                    // Stillness counts the whole path, so drifting back does not re-arm a hold.
                    if (IsEnabled(InputSlot.Drag) &&
                        (moved > tapSlop || (IsEnabled(InputSlot.HoldDrag) && held >= holdTime && pathLength > holdStillness)))
                    {
                        if (ActionFor(InputSlot.Drag) == InputAction.LassoSelect)
                            BeginLasso(sample.Position, armed: false);
                        else
                        {
                            State = GestureState.Panning;
                            Emit(GestureEventKind.Cancelled);
                            Emit(GestureEventKind.PanBegin, downPos);
                            Emit(GestureEventKind.PanUpdate, sample.Position);
                        }
                    }
                    else if (IsEnabled(InputSlot.HoldDrag) && held >= holdTime && pathLength <= holdStillness)
                    {
                        if (ActionFor(InputSlot.HoldDrag) == InputAction.Pan)
                        {
                            State = GestureState.Panning;
                            Emit(GestureEventKind.Cancelled);
                            Emit(GestureEventKind.PanBegin, downPos);
                            Emit(GestureEventKind.PanUpdate, sample.Position);
                        }
                        else BeginLasso(sample.Position, armed: true);
                    }
                    break;
                case GestureState.Panning: Emit(GestureEventKind.PanUpdate, sample.Position); break;
                case GestureState.LassoArmed:
                case GestureState.Lassoing:
                    if (Append(sample.Position))
                    {
                        State = GestureState.Lassoing;
                        Emit(GestureEventKind.LassoPoint, sample.Position);
                    }
                    break;
            }
        }

        private bool Append(GesturePoint point)
        {
            if (points.Count >= maxPoints) return false;
            if (points.Count > 0 && GesturePoint.Distance(points[points.Count - 1], point) < decimation) return false;
            points.Add(point);
            return true;
        }

        private void BeginLasso(GesturePoint current, bool armed)
        {
            State = armed ? GestureState.LassoArmed : GestureState.Lassoing;
            points.Clear();
            Append(downPos);
            Emit(GestureEventKind.Cancelled);
            Emit(GestureEventKind.LassoBegin, downPos);
            if (!armed && Append(current)) Emit(GestureEventKind.LassoPoint, current);
        }

        private void End(PointerSample sample)
        {
            switch (State)
            {
                case GestureState.Pending:
                    if (!tapExceededSlop && GesturePoint.Distance(sample.Position, downPos) <= tapSlop && sample.Time - downTime < holdTime)
                        EmitTap(sample.Time);
                    else Emit(GestureEventKind.Cancelled, downPos);
                    break;
                case GestureState.Panning: Emit(GestureEventKind.PanEnd); break;
                case GestureState.LassoArmed:
                case GestureState.Lassoing: Emit(GestureEventKind.LassoComplete); break;
            }
            State = GestureState.Idle;
        }

        // The first tap of a pair is a normal tap and has already fired. The second
        // release replaces its own Tap with the double-tap action, which is safe
        // because only ground and villagers qualify: tap one cleared or selected,
        // and the action overwrites whatever tap two would have done.
        private void EmitTap(float time)
        {
            bool eligible = downClass == GestureTargetClass.None || IsVillager(downClass);
            InputSlot slot = IsVillager(downClass) ? InputSlot.DoubleTapVillager : InputSlot.DoubleTapGround;
            if (doubleCandidate && IsEnabled(slot))
            {
                lastTapValid = false;
                Emit(slot == InputSlot.DoubleTapVillager
                    ? GestureEventKind.DoubleTapVillager : GestureEventKind.DoubleTapGround, downPos);
                return;
            }
            Emit(GestureEventKind.Tap, downPos);
            lastTapValid = eligible;
            lastTapTime = time;
            lastTapPos = downPos;
            lastTapClass = downClass;
            lastTapId = downId;
        }

        private static bool IsVillager(GestureTargetClass target) =>
            target == GestureTargetClass.Villager || target == GestureTargetClass.SelectedVillager;

        // Consumes the remembered tap: a press either continues it or ends the chain.
        private bool MatchesLastTap(PointerSample press)
        {
            if (!lastTapValid) return false;
            lastTapValid = false;
            float gap = press.Time - lastTapTime;
            if (gap < 0f || gap > DoubleTapTime) return false;
            if (GesturePoint.Distance(press.Position, lastTapPos) > DoubleTapRadiusMm) return false;
            if (IsVillager(press.Target)) return IsVillager(lastTapClass) && press.TargetId == lastTapId;
            return press.Target == GestureTargetClass.None && lastTapClass == GestureTargetClass.None;
        }

        public void Cancel()
        {
            lastTapValid = false;
            if (State == GestureState.Idle) return;
            bool drawing = State == GestureState.LassoArmed || State == GestureState.Lassoing;
            if (State == GestureState.Panning) Emit(GestureEventKind.PanEnd);
            if (PairActive) EndPair(cancelled: true);
            State = GestureState.Cancelled;
            points.Clear();
            Emit(GestureEventKind.Cancelled);
            if (drawing) Emit(GestureEventKind.LassoComplete);
            State = GestureState.Idle;
        }

        private void EndPair(bool cancelled = false)
        {
            if (pairPan) Emit(GestureEventKind.PanEnd);
            if (pairLasso)
            {
                if (cancelled) points.Clear();
                Emit(GestureEventKind.LassoComplete);
            }
            if (pairZoom) Emit(GestureEventKind.ZoomEnd);
            pairPan = pairLasso = pairZoom = false;
            State = GestureState.Idle;
        }
    }
}
