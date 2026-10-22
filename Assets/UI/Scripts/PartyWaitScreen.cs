using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class PartyWaitScreen : IDisposable
    {
        private readonly VisualElement root = new VisualElement();
        private readonly LobbyOverlayRouter router;
        private readonly SafeAreaBinder safeArea;
        private readonly VisualElement slots;
        private readonly Button random, invite;
        private readonly Label invitee;
        private PartyWaitModel model;
        private double openedAt;
        private bool announcedReady;
        public bool IsOpen { get; private set; }
        public int Filled => model?.Filled ?? 0;
        public event Action InviteRequested;

        public PartyWaitScreen(VisualElement shell, LobbyOverlayRouter router)
        {
            this.router = router;
            Resources.Load<VisualTreeAsset>("Lobby/PartyWait")?.CloneTree(root);
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.display = DisplayStyle.None;
            shell.Add(root);
            slots = root.Q<VisualElement>("party-slots");
            invitee = root.Q<Label>("party-invitee");
            random = root.Q<Button>("party-random");
            invite = root.Q<Button>("party-invite");
            var cancel = root.Q<Button>("party-cancel");
            if (cancel != null) cancel.clicked += Cancel;
            if (invite != null) invite.clicked += () => InviteRequested?.Invoke();
            if (random != null) random.clicked += () =>
            {
                if (model?.CanQueueRandom != true) return;
                Debug.Log("[Party] 2v2 random queue (not implemented)");
                Cancel();
            };
            safeArea = new SafeAreaBinder(root.Q<VisualElement>("party-safe-area"));
        }

        public void Open(LobbyMode mode, string player, int filled = 1)
        {
            if (IsOpen) Cancel();
            model = new PartyWaitModel(mode);
            model.SetFilled(filled);
            openedAt = Time.realtimeSinceStartupAsDouble;
            announcedReady = false;
            IsOpen = true;
            root.style.display = DisplayStyle.Flex;
            root.BringToFront();
            root.parent?.Q<VisualElement>("toast-dock")?.BringToFront();
            if (invitee != null) invitee.text = "Invitee: " + player;
            if (invite != null) invite.style.display = mode == LobbyMode.FreeForAll4 ? DisplayStyle.Flex : DisplayStyle.None;
            router.Open(new LobbyOverlay("party-wait", OverlayKind.Fullscreen, true, true,
                () => { Cancel(); return true; }), Cancel);
            RenderSlots();
            Update();
        }

        public void SetFilled(int count)
        {
            if (!IsOpen) return;
            model.SetFilled(count);
            RenderSlots();
        }

        private void RenderSlots()
        {
            slots?.Clear();
            for (int i = 0; i < model.Capacity; i++)
            {
                var slot = new PaperCard { name = "party-slot-" + i, pickingMode = PickingMode.Ignore };
                slot.Add(new Label(i == 0 ? "You" : i < model.Filled ? "Player" : "Waiting"));
                slot.AddToClassList("lb-party-slot");
                slot.EnableInClassList("lb-party-slot-filled", i < model.Filled);
                slots?.Add(slot);
            }
            if (invite != null) invite.SetEnabled(!model.BattleReady);
            if (model.BattleReady && !announcedReady)
            {
                announcedReady = true;
                Debug.Log("[Party] Full 1v1v1v1 party: battle loading would start (not implemented)");
            }
        }

        public void Update()
        {
            if (!IsOpen) return;
            safeArea.Update();
            model.Advance((long)((Time.realtimeSinceStartupAsDouble - openedAt) * 1000));
            if (random != null) random.style.display = model.CanQueueRandom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            model.Cancel();
            IsOpen = false;
            root.style.display = DisplayStyle.None;
            router.Closed("party-wait");
        }

        public void Dispose() { Cancel(); root.RemoveFromHierarchy(); }
    }
}
