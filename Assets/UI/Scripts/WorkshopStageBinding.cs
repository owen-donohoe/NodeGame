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
            shell.RegisterCallback<PointerDownEvent>(Tap, TrickleDown.TrickleDown);
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
        private void Tap(PointerDownEvent evt)
        {
            if (!page.Replacing) return;
            for (var target = evt.target as VisualElement; target != null; target = target.parent)
            {
                if (target.ClassListContains("ws-district-slot") || target.name == "workshop-info-use") return;
            }
            page.CancelReplacement();
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
            shell.UnregisterCallback<PointerDownEvent>(Tap, TrickleDown.TrickleDown);
        }
    }
}

