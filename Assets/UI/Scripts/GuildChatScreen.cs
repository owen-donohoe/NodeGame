using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class GuildChatScreen
    {
        private readonly IGuildService service;
        private readonly LobbyOverlayRouter router;
        private readonly GuildDataView messages = new GuildDataView();
        private readonly ScrollView list;
        private readonly TextField input;
        private readonly Button send;
        private readonly Label error;
        private readonly SafeAreaBinder safeArea;
        private readonly IVisualElementScheduledItem poll;
        private int loading;
        private int renderedCount = -1;
        private string lastMessage;
        private bool stick = true;
        private bool sending;
        private int generation;
        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }

        public GuildChatScreen(IGuildService service, LobbyOverlayRouter router, VisualElement host)
        {
            this.service = service; this.router = router;
            Root = new VisualElement { name = "guild-chat-screen" };
            Resources.Load<VisualTreeAsset>("Lobby/GuildChat").CloneTree(Root);
            Root.AddToClassList("lb-guild-chat");
            Root.AddToClassList("lb-no-swipe");
            Root.style.display = DisplayStyle.None;
            host.Add(Root);
            safeArea = new SafeAreaBinder(Root.Q("guild-chat-safe"));
            list = Root.Q<ScrollView>("guild-chat-list");
            list.Add(messages);
            input = Root.Q<TextField>("guild-chat-input");
            input.Query<TextElement>().ForEach(element => element.enableRichText = false);
            send = Root.Q<Button>("guild-chat-send");
            error = Root.Q<Label>("guild-chat-error");
            error.enableRichText = false;
            send.clicked += () => _ = SendAsync();
            Root.Q<Button>("guild-chat-close").clicked += Close;
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { _ = SendAsync(); evt.StopPropagation(); }
            });
            messages.RetryRequested += Refresh;
            messages.RegisterCallback<GeometryChangedEvent>(evt => { if (stick) StickToBottom(); });
            list.RegisterCallback<PointerDownEvent>(evt => stick = false, TrickleDown.TrickleDown);
            list.RegisterCallback<WheelEvent>(evt => stick = false, TrickleDown.TrickleDown);
            poll = Root.schedule.Execute(() => { if (IsOpen) { safeArea.Update(); Refresh(); } }).Every(2000);
            poll.Pause();
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) Close(); });
        }

        public void Open(GuildView guild)
        {
            if (guild?.Summary == null) return;
            generation++;
            Root.Q("guild-chat-header").Clear();
            Root.Q("guild-chat-header").Add(GuildViews.Header(guild));
            error.text = "";
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            safeArea.Update();
            BackendServices.Account.Changed -= AccountChanged;
            BackendServices.Account.Changed += AccountChanged;
            router.Open(new LobbyOverlay("guild-chat", OverlayKind.Fullscreen, true, true), Close);
            renderedCount = -1; stick = true;
            _ = LoadAsync(); poll.Resume();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; generation++;
            poll.Pause(); messages.Invalidate(true);
            input.SetValueWithoutNotify("");
            Root.style.display = DisplayStyle.None;
            BackendServices.Account.Changed -= AccountChanged;
            router.Closed("guild-chat");
        }

        private void AccountChanged(AccountInfo info) => Close();
        private void Refresh() { if (IsOpen && loading == 0) _ = LoadAsync(); }

        private async Task LoadAsync()
        {
            loading++;
            try
            {
                await messages.Load(service.GetChatAsync, RenderMessages, rows => rows != null && rows.Count > 0,
                    "No messages yet. Say hello!");
            }
            finally { loading--; }
        }

        private void RenderMessages(List<ChatMessage> rows)
        {
            string latest = rows.Count == 0 ? null : rows[rows.Count - 1]?.MessageId;
            if (rows.Count != renderedCount || latest != lastMessage) stick = true;
            renderedCount = rows.Count; lastMessage = latest;
            foreach (var row in rows)
            {
                if (row == null) continue;
                messages.Content.Add(PlayerCardViews.Text(PlayerCardViews.Handle(row.Sender) + ": " + row.Text));
            }
            if (stick) list.schedule.Execute(StickToBottom);
        }

        private void StickToBottom()
        {
            if (IsOpen) list.scrollOffset = new Vector2(0f, list.verticalScroller.highValue);
        }

        private async Task SendAsync()
        {
            if (!IsOpen || sending) return;
            string draft = input.value;
            if (!GuildChatText.TryNormalize(draft, out string text)) { error.text = "Enter a message."; return; }
            int token = generation;
            string account = BackendServices.Account.Current?.PlayerId;
            sending = true; send.SetEnabled(false); error.text = "";
            try
            {
                await service.SendChatAsync(text);
                if (!IsOpen || token != generation || account != BackendServices.Account.Current?.PlayerId) return;
                if (input.value == draft) input.SetValueWithoutNotify("");
                await LoadAsync();
            }
            catch (Exception)
            {
                if (IsOpen && token == generation && account == BackendServices.Account.Current?.PlayerId)
                    error.text = "Couldn't send. Your message is still here; try again.";
            }
            finally { sending = false; send.SetEnabled(true); }
        }
    }
}
