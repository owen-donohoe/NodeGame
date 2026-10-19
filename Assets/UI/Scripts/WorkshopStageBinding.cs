using System;
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

        public WorkshopStageBinding(WorkshopPage page, LobbyStage stage, LobbyOverlayRouter router,
            VisualElement shell, Action refresh)
        {
            this.page = page; this.stage = stage; this.router = router; this.shell = shell; this.refresh = refresh;
            page.ItemViewed += View;
            page.DistrictUseRequested += Begin;
            page.DistrictSlotPicked += Pick;
            page.ReplacementChanged += ReplacementChanged;
            page.LoadoutChanged += refresh;
            stage.DistrictUsed += Used;
            router.Stack.Changed += StackChanged;
            shell.RegisterCallback<PointerDownEvent>(Tap, TrickleDown.TrickleDown);
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
            page.CancelReplacement(); stage.Deselect(); stage.ClearFocus();
            page.ItemViewed -= View; page.DistrictUseRequested -= Begin; page.DistrictSlotPicked -= Pick;
            page.ReplacementChanged -= ReplacementChanged; page.LoadoutChanged -= refresh;
            stage.DistrictUsed -= Used; router.Stack.Changed -= StackChanged;
            shell.UnregisterCallback<PointerDownEvent>(Tap, TrickleDown.TrickleDown);
        }
    }
}
