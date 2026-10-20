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
        private readonly Action refresh;
        private readonly NavigationController navigation;
        private readonly IVisualElementScheduledItem projection;
        private int pointer = -1;
        private Vector2 pointerStart;
        private long pointerStartedAt;
        private bool dragged;

        public WorkshopStageBinding(WorkshopPage page, LobbyStage stage, LobbyOverlayRouter router,
            VisualElement shell, Action refresh, NavigationController navigation = null)
        {
            this.page = page; this.stage = stage; this.router = router; this.refresh = refresh;
            this.navigation = navigation;
            if (navigation != null) navigation.PageShown += PageShown;
            projection = page.Root.schedule.Execute(UpdateProjection).Every(16); projection.Pause();
            page.Hidden += Hide;
            page.ItemViewed += View;
            page.ViewChanged += ShowView;
            page.ReplacementChanged += ReplacementChanged;
            page.LoadoutChanged += refresh;
            stage.DistrictUsed += Used;
            router.Stack.Changed += StackChanged;
            page.Root.RegisterCallback<PointerDownEvent>(PointerDown, TrickleDown.TrickleDown);
            page.Root.RegisterCallback<PointerMoveEvent>(PointerMove, TrickleDown.TrickleDown);
            page.Root.RegisterCallback<PointerUpEvent>(PointerUp, TrickleDown.TrickleDown);
            page.Root.RegisterCallback<PointerCancelEvent>(PointerCancel, TrickleDown.TrickleDown);
        }

        private Vector2 ScreenPoint(Vector2 panelPoint)
        {
            Rect panel = page.Root.panel.visualTree.worldBound;
            return new Vector2(panelPoint.x * Screen.width / panel.width,
                Screen.height - panelPoint.y * Screen.height / panel.height);
        }
        private void Hide()
        {
            projection.Pause(); ResetPointer(); stage.Deselect(); stage.ClearFocus();
        }

        private void PageShown(LobbyPageID id)
        {
            if (id == LobbyPageID.Workshop) { UpdateProjection(); ShowView(page.ViewingSuit); projection.Resume(); }
        }

        private void UpdateProjection()
        {
            if (!page.IsOpen || page.Root.panel == null) return;
            var panel = page.Root.panel.visualTree.worldBound;
            stage.SetWorkshopBand((page.StageViewportTop - panel.y + 12f) / panel.height, 0.55f);
            if (stage.TryGetClusterViewportBounds(out Rect bounds)) page.SetStageViewportBottom(1f - bounds.yMin);
        }

        private void ShowView(bool suits)
        {
            stage.ShowShot(suits ? StageShot.Suits : StageShot.Districts);
            UpdateProjection();
        }

        public void RefreshInspection() => View(page.InspectedSuit, page.ViewedId);
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
            if (suit) stage.FocusSuit(slot);
            else { stage.ShowShot(StageShot.Districts); stage.SelectDistrict(slot); }
        }
        private void Used(DistrictClusterResult result)
        {
            if (result.Action == DistrictClusterAction.Replaced)
                page.CompleteReplacement(StageSlotMap.ToChosen(page.StageLoadout.districtIDs, result.Slot));
        }
        private void ReplacementChanged()
        {
            if (page.Replacing)
            {
                if (page.ReplacingSuit) stage.BeginSuitReplace();
                else { stage.SetDistrictCapacity(page.StageLoadout.districtIDs.Length); stage.BeginReplace(); }
                router.Open(new LobbyOverlay(ReplaceRoute, OverlayKind.Menu, backHandler: () =>
                {
                    page.CancelReplacement(); return true;
                }), page.CancelReplacement);
            }
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
            if (evt.target != page.StageWindow || pointer != -1 || evt.button != 0 || !page.IsOpen ||
                router.Stack.Top != null && router.Stack.Top.ID != ReplaceRoute) return;
            pointer = evt.pointerId;
            pointerStart = ScreenPoint(evt.position);
            pointerStartedAt = evt.timestamp;
            dragged = false;
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
            if (!tap || !page.IsOpen || router.Stack.Top != null && router.Stack.Top.ID != ReplaceRoute ||
                !stage.TryPick(ScreenPoint(evt.position), out StagePick pick)) return;
            if (page.Replacing)
            {
                if (page.ReplacingSuit)
                {
                    if (pick.IsSuit) page.CompleteSuitReplacement(StageSlotMap.ToChosen(page.StageLoadout.suitIDs, pick.Index));
                }
                else if (!pick.IsSuit) stage.PickReplace(pick.Index);
                return;
            }
            page.InspectStageItem(pick.IsSuit, pick.Index);
        }
        private void PointerCancel(PointerCancelEvent evt) => ResetPointer();
        private void ResetPointer()
        {
            pointer = -1;
        }
        public void Dispose()
        {
            page.Hidden -= Hide;
            if (navigation != null) navigation.PageShown -= PageShown;
            Hide();
            page.CancelReplacement(); stage.Deselect(); stage.ClearFocus();
            page.ViewChanged -= ShowView;
            page.ItemViewed -= View;
            page.ReplacementChanged -= ReplacementChanged; page.LoadoutChanged -= refresh;
            stage.DistrictUsed -= Used; router.Stack.Changed -= StackChanged;
            page.Root.UnregisterCallback<PointerDownEvent>(PointerDown, TrickleDown.TrickleDown);
            page.Root.UnregisterCallback<PointerMoveEvent>(PointerMove, TrickleDown.TrickleDown);
            page.Root.UnregisterCallback<PointerUpEvent>(PointerUp, TrickleDown.TrickleDown);
            page.Root.UnregisterCallback<PointerCancelEvent>(PointerCancel, TrickleDown.TrickleDown);
        }
    }
}
