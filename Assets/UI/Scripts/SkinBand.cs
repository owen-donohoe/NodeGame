using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Endlessly looping horizontal band of skin tiles. Thirteen tiles are allocated once and
    /// repositioned from <see cref="SkinBandMath"/>; the update path allocates nothing.
    /// Drag scrolls, a tap centres a tile, release or cancel snaps to the nearest skin.
    /// </summary>
    public sealed class SkinBand : VisualElement
    {
        public const float TilePitch = 56f;
        private const float TileSize = 48f;
        private const float TapSlop = 6f;
        private const double EaseRate = 0.28;
        private const double SettleEpsilon = 0.002;

        public event Action<int> CenterChanged;

        private readonly VisualElement[] tiles = new VisualElement[SkinBandMath.VisibleCount];
        private readonly Label[] tileLabels = new Label[SkinBandMath.VisibleCount];
        private readonly int[] shownChoice = new int[SkinBandMath.VisibleCount];
        private readonly SkinBandSlot[] slots = new SkinBandSlot[SkinBandMath.VisibleCount];
        private readonly IVisualElementScheduledItem scheduled;
        private string[] monograms = Array.Empty<string>();
        private bool[] owned = Array.Empty<bool>();
        private int count;
        private double position, target;
        private bool animating, dragging;
        private int pointerId = -1;
        private float pressX, lastX;
        private bool moved;
        private int reportedCenter = -1;

        public int CenteredIndex => SkinBandMath.CenterIndex(position, count);

        public SkinBand()
        {
            AddToClassList("ws-band");
            pickingMode = PickingMode.Position;
            style.overflow = Overflow.Hidden;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = new PaperCard { pickingMode = PickingMode.Ignore };
                tile.AddToClassList("ws-band-tile");
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("ws-monogram"); label.AddToClassList("ui-w600");
                tile.Add(label);
                tile.style.position = Position.Absolute;
                tile.style.width = TileSize; tile.style.height = TileSize;
                tile.style.top = 4;
                tiles[i] = tile; tileLabels[i] = label; shownChoice[i] = -2;
                Add(tile);
            }
            scheduled = schedule.Execute(Tick).Every(16);
            scheduled.Pause();
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            RegisterCallback<GeometryChangedEvent>(OnGeometry);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>Replaces the choices and centres one of them without firing CenterChanged.</summary>
        public void SetChoices(LoadoutChoice[] choices, int centeredIndex)
        {
            Stop();
            count = choices == null ? 0 : choices.Length;
            if (monograms.Length != count) { monograms = new string[count]; owned = new bool[count]; }
            for (int i = 0; i < count; i++)
            {
                string label = choices[i].Label ?? "";
                monograms[i] = label.Length > 2 ? label.Substring(0, 2).ToUpperInvariant() : label.ToUpperInvariant();
                owned[i] = choices[i].UnlockState.Status == UnlockStatus.Owned;
            }
            position = target = count == 0 ? 0 : SkinBandMath.Wrap(centeredIndex, count);
            reportedCenter = CenteredIndex;
            for (int i = 0; i < shownChoice.Length; i++) shownChoice[i] = -2;
            Layout();
        }

        /// <summary>Halts animation and any drag, releasing pointer capture, then rests on the nearest skin.</summary>
        public void Stop()
        {
            scheduled.Pause();
            animating = false;
            if (pointerId >= 0 && this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
            pointerId = -1;
            dragging = false;
            if (count > 0)
            {
                position = target = SkinBandMath.Snap(SkinBandMath.Rebase(position, count));
                Layout();
            }
        }

        private void OnDetach(DetachFromPanelEvent evt) { Stop(); }
        private void OnGeometry(GeometryChangedEvent evt) { Layout(); }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (count == 0 || dragging) return;
            pointerId = evt.pointerId; dragging = true; moved = false;
            pressX = lastX = evt.localPosition.x;
            animating = false; scheduled.Pause();
            position = SkinBandMath.Rebase(position, count);
            target = position;
            this.CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId) return;
            float x = evt.localPosition.x;
            if (!moved && Mathf.Abs(x - pressX) > TapSlop) moved = true;
            if (moved && count > 1)
            {
                position += SkinBandMath.DragDelta(x - lastX, TilePitch);
                Layout();
            }
            lastX = x;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId) return;
            float x = evt.localPosition.x;
            EndDrag();
            if (!moved && count > 1)
            {
                double offset = (x - layout.width * 0.5f) / TilePitch;
                target = SkinBandMath.Snap(position + offset);
            }
            else target = SkinBandMath.Snap(position);
            BeginSettle();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId) return;
            EndDrag();
            target = SkinBandMath.Snap(position);
            BeginSettle();
        }

        // Capture lost without Up or Cancel (e.g. the element left the panel): still snap.
        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (!dragging) return;
            EndDrag();
            target = SkinBandMath.Snap(position);
            BeginSettle();
        }

        private void EndDrag()
        {
            int id = pointerId;
            dragging = false; pointerId = -1;
            if (id >= 0 && this.HasPointerCapture(id)) this.ReleasePointer(id);
        }

        private void BeginSettle()
        {
            animating = true;
            scheduled.Resume();
        }

        private void Tick()
        {
            if (!animating) { scheduled.Pause(); return; }
            double remaining = target - position;
            if (remaining < SettleEpsilon && remaining > -SettleEpsilon)
            {
                position = target = SkinBandMath.Rebase(target, count);
                animating = false;
                scheduled.Pause();
                Layout();
                Report();
                return;
            }
            position += remaining * EaseRate;
            Layout();
        }

        private void Report()
        {
            int center = CenteredIndex;
            if (center == reportedCenter) return;
            reportedCenter = center;
            CenterChanged?.Invoke(center);
        }

        private void Layout()
        {
            SkinBandMath.Fill(position, count, slots);
            float middle = layout.width * 0.5f;
            if (float.IsNaN(middle)) middle = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                SkinBandSlot slot = slots[i];
                VisualElement tile = tiles[i];
                bool show = slot.ChoiceIndex >= 0 && (count > 1 || Math.Abs(slot.Offset) < 0.5);
                tile.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                tile.style.left = middle + (float)slot.Offset * TilePitch - TileSize * 0.5f;
                tile.style.opacity = (float)slot.Alpha;
                float scale = (float)slot.Scale * (Math.Abs(slot.Offset) < 0.5 ? 1.2f : 1f);
                tile.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
                if (shownChoice[i] != slot.ChoiceIndex)
                {
                    shownChoice[i] = slot.ChoiceIndex;
                    tileLabels[i].text = monograms[slot.ChoiceIndex];
                    tile.EnableInClassList("ws-art--neutral", !owned[slot.ChoiceIndex]);
                }
                tile.EnableInClassList("ws-band-tile--center", Math.Abs(slot.Offset) < 0.5);
            }
        }
    }
}
