using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class WorkshopStageBinding : IDisposable
    {
        private const string ReplaceRoute = "workshop-replacing";
        private readonly WorkshopPage page;
        private readonly LobbyStage stage;
        private readonly LobbyOverlayRouter router;
        private readonly VisualElement shell;
        private readonly Action refresh;
        private readonly IVisualElementScheduledItem framing;
        private Rect lastWindow;
        private Vector2 lastScreen;
        private int pointer = -1;
        private Vector2 pointerStart;
        private long pointerStartedAt;
        private bool dragged;

        public WorkshopStageBinding(WorkshopPage page, LobbyStage stage, LobbyOverlayRouter router,
            VisualElement shell, Action refresh)
        {
            this.page = page; this.stage = stage; this.router = router; this.shell = shell; this.refresh = refresh;
            page.Hidden += Hide;
            framing = page.Root.schedule.Execute(FrameWindow).Every(16);
            page.ItemViewed += View;
            page.DistrictUseRequested += Begin;
            page.DistrictSlotPicked += Pick;
            page.ReplacementChanged += ReplacementChanged;
            page.LoadoutChanged += refresh;
            stage.DistrictUsed += Used;
            router.Stack.Changed += StackChanged;
            page.StageWindow.RegisterCallback<PointerDownEvent>(PointerDown);
            page.StageWindow.RegisterCallback<PointerMoveEvent>(PointerMove);
            page.StageWindow.RegisterCallback<PointerUpEvent>(PointerUp);
            page.StageWindow.RegisterCallback<PointerCancelEvent>(PointerCancel);
        }

        private Vector2 ScreenPoint(Vector2 panelPoint)
        {
            Rect panel = page.Root.panel.visualTree.worldBound;
            return new Vector2(panelPoint.x * Screen.width / panel.width,
                Screen.height - panelPoint.y * Screen.height / panel.height);
        }
        private void FrameWindow()
        {
            if (!page.IsOpen || page.StageWindow?.panel == null) return;
            var window = page.StageWindow;
            float height = page.Root.panel.visualTree.worldBound.height * 0.4f;
            if (!Mathf.Approximately(window.resolvedStyle.height, height)) window.style.height = height;
            Rect bounds = window.worldBound;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (bounds == lastWindow && screen == lastScreen) return;
            lastWindow = bounds; lastScreen = screen;
            stage.SetFocusRect(Rect.MinMaxRect(ScreenPoint(bounds.min).x, ScreenPoint(bounds.max).y,
                ScreenPoint(bounds.max).x, ScreenPoint(bounds.min).y));
        }
        private void Hide()
        {
            ResetPointer();
            lastWindow = default;
            stage.ClearFocusRect();
        }

        public void RefreshInspection() => View(page.ViewingSuit, page.ViewedId);
        public void LoadoutReset() => page.CancelReplacement();
        private void View(bool suit, string id)
        {
            if (page.Replacing) return;
            stage.Deselect(); stage.ClearFocus();
            var data = page.StageLoadout;
            stage.SetDistrictCapacity(data.districtIDs.Length);
            string[] chosen = suit ? data.suitIDs : data.districtIDs;
            int slot = StageSlotMap.ToStage(chosen, Array.IndexOf(chosen, id));
            if (slot < 0) return;
            if (suit) stage.FocusSuit(slot); else stage.SelectDistrict(slot);
        }
        private bool Begin()
        {
            stage.SetDistrictCapacity(page.StageLoadout.districtIDs.Length);
            return stage.BeginReplace();
        }
        private void Pick(int slot)
        {
            int stageSlot = StageSlotMap.ToStage(page.StageLoadout.districtIDs, slot);
            if (stageSlot >= 0) stage.PickReplace(stageSlot);
        }
        private void Used(DistrictClusterResult result)
        {
            if (result.Action == DistrictClusterAction.Replaced)
                page.CompleteReplacement(StageSlotMap.ToChosen(page.StageLoadout.districtIDs, result.Slot));
        }
        private void ReplacementChanged()
        {
            if (page.Replacing)
                router.Open(new LobbyOverlay(ReplaceRoute, OverlayKind.Menu, backHandler: () =>
                {
                    page.CancelReplacement(); return true;
                }), page.CancelReplacement);
            else
            {
                stage.CancelReplace();
                router.Closed(ReplaceRoute);
            }
        }
        private void StackChanged()
        {
            if (page.Replacing && router.Stack.Top?.ID != ReplaceRoute) page.CancelReplacement();
        }
        private void PointerDown(PointerDownEvent evt)
        {
            if (pointer != -1 || evt.button != 0 || !page.IsOpen ||
                router.Stack.Top != null && router.Stack.Top.ID != ReplaceRoute) return;
            pointer = evt.pointerId;
            pointerStart = ScreenPoint(evt.position);
            pointerStartedAt = evt.timestamp;
            dragged = false;
            page.StageWindow.CapturePointer(pointer);
            evt.StopPropagation();
        }
        private void PointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == pointer && (ScreenPoint(evt.position) - pointerStart).sqrMagnitude > 100f) dragged = true;
        }
        private void PointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            bool tap = !dragged && evt.timestamp - pointerStartedAt <= 500 &&
                (ScreenPoint(evt.position) - pointerStart).sqrMagnitude <= 100f &&
                page.StageWindow.worldBound.Contains(evt.position);
            ResetPointer();
            evt.StopPropagation();
            if (!tap || !page.IsOpen || router.Stack.Top != null && router.Stack.Top.ID != ReplaceRoute ||
                !stage.TryPick(ScreenPoint(evt.position), out StagePick pick)) return;
            if (page.Replacing)
            {
                if (!pick.IsSuit) stage.PickReplace(pick.Index);
                return;
            }
            page.InspectStageItem(pick.IsSuit, pick.Index);
        }
        private void PointerCancel(PointerCancelEvent evt) => ResetPointer();
        private void ResetPointer()
        {
            if (pointer >= 0) page.StageWindow.ReleasePointer(pointer);
            pointer = -1;
        }
        public void Dispose()
        {
            framing.Pause();
            page.Hidden -= Hide;
            Hide();
            page.CancelReplacement(); stage.Deselect(); stage.ClearFocus();
            page.ItemViewed -= View; page.DistrictUseRequested -= Begin; page.DistrictSlotPicked -= Pick;
            page.ReplacementChanged -= ReplacementChanged; page.LoadoutChanged -= refresh;
            stage.DistrictUsed -= Used; router.Stack.Changed -= StackChanged;
            page.StageWindow.UnregisterCallback<PointerDownEvent>(PointerDown);
            page.StageWindow.UnregisterCallback<PointerMoveEvent>(PointerMove);
            page.StageWindow.UnregisterCallback<PointerUpEvent>(PointerUp);
            page.StageWindow.UnregisterCallback<PointerCancelEvent>(PointerCancel);
        }
    }
}


