using System;
using System.Collections.Generic;
using NodeWar.Lobby;

namespace NodeWar.Input
{
    public enum GestureState { Idle, Pending, Panning, LassoArmed, Lassoing, Blocked, Pinching, Cancelled }
    public enum PointerButton { Primary, Touch, Secondary, Middle, Scroll }
    public enum PointerPhase { None, Began, Held, Ended, Cancelled }
    public enum GestureEventKind
    {
        PointerDown, Cancelled, Tap, SecondaryClick, PanBegin, PanUpdate, PanEnd,
        LassoBegin, LassoPoint, LassoComplete, ZoomBegin, ZoomUpdate, ZoomEnd
    }

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
        public PointerSample(float time, GesturePoint position, int pointerId, PointerButton button,
            PointerPhase phase, bool overUI = false)
        {
            Time = time; Position = position; PointerId = pointerId;
            Button = button; Phase = phase; OverUI = overUI;
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
        private readonly List<GesturePoint> points = new List<GesturePoint>();

        public GestureState State { get; private set; }
        public bool PanSuppressed => State == GestureState.LassoArmed || State == GestureState.Lassoing;
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
            tapSlop = tapSlopMm;
            this.holdTime = holdTime;
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
        }

        public bool IsEnabled(InputSlot slot) => bindings[(int)slot].enabled;
        public InputAction ActionFor(InputSlot slot) => (InputAction)bindings[(int)slot].action;

        public void ProcessFrame(IReadOnlyList<PointerSample> samples)
        {
            bool hasPrimary = false;
            PointerSample primary = default;
            int touches = 0;
            GesturePoint a = default, b = default;
            for (int i = 0; i < samples.Count; i++)
            {
                PointerSample sample = samples[i];
                if (sample.Button == PointerButton.Secondary && sample.Phase == PointerPhase.Began && !sample.OverUI)
                    Emit(GestureEventKind.SecondaryClick, sample.Position);
                if (sample.Button == PointerButton.Primary) { primary = sample; hasPrimary = true; }
                if (sample.Button != PointerButton.Touch ||
                    (sample.Phase != PointerPhase.Began && sample.Phase != PointerPhase.Held)) continue;
                if (touches == 0) a = sample.Position;
                if (touches == 1) b = sample.Position;
                touches++;
            }

            if (!hasPrimary) return;
            if (touches >= 2)
            {
                float span = GesturePoint.Distance(a, b);
                if (State == GestureState.Pinching)
                {
                    if (span > minPinchSpan && Math.Abs(span - pinchStartSpan) >= pinchDeadZone)
                        Emit(GestureEventKind.ZoomUpdate, scale: span / pinchStartSpan);
                    return;
                }
                if (State == GestureState.Blocked) return;
                if (PanSuppressed)
                {
                    Cancel();
                    State = GestureState.Blocked;
                    return;
                }
                if (!IsEnabled(InputSlot.Pinch))
                {
                    Cancel();
                    State = GestureState.Blocked;
                    return;
                }
                if (span <= minPinchSpan) return;
                if (State == GestureState.Panning) Emit(GestureEventKind.PanEnd);
                if (State != GestureState.Idle) Emit(GestureEventKind.Cancelled);
                State = GestureState.Pinching;
                pinchStartSpan = span;
                points.Clear();
                Emit(GestureEventKind.ZoomBegin);
                return;
            }
            if (State == GestureState.Pinching)
            {
                State = GestureState.Idle;
                Emit(GestureEventKind.ZoomEnd);
                return;
            }

            switch (primary.Phase)
            {
                case PointerPhase.Began:
                    downPos = primary.Position;
                    downTime = primary.Time;
                    previousPos = downPos;
                    pathLength = 0f;
                    tapExceededSlop = false;
                    points.Clear();
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
                    Emit(!tapExceededSlop && GesturePoint.Distance(sample.Position, downPos) <= tapSlop && sample.Time - downTime < holdTime
                        ? GestureEventKind.Tap : GestureEventKind.Cancelled, downPos);
                    break;
                case GestureState.Panning: Emit(GestureEventKind.PanEnd); break;
                case GestureState.LassoArmed:
                case GestureState.Lassoing: Emit(GestureEventKind.LassoComplete); break;
            }
            State = GestureState.Idle;
        }

        public void Cancel()
        {
            if (State == GestureState.Idle) return;
            bool drawing = PanSuppressed;
            if (State == GestureState.Panning) Emit(GestureEventKind.PanEnd);
            if (State == GestureState.Pinching) Emit(GestureEventKind.ZoomEnd);
            State = GestureState.Cancelled;
            points.Clear();
            Emit(GestureEventKind.Cancelled);
            if (drawing) Emit(GestureEventKind.LassoComplete);
            State = GestureState.Idle;
        }
    }
}
