using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal sealed class GuildReportScreen
    {
        private readonly LobbyOverlayRouter router;
        private readonly SafeAreaBinder safeArea;
        private readonly VisualElement body;
        private bool open;
        public VisualElement Root { get; }

        public GuildReportScreen(LobbyOverlayRouter router, VisualElement host)
        {
            this.router = router;
            Root = new VisualElement { name = "guild-report-screen" };
            Root.AddToClassList("mk-card"); Root.AddToClassList("lb-guild-report"); Root.AddToClassList("lb-no-swipe");
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Guild"));
            body = new ScrollView(); body.AddToClassList("lb-guild-report-safe"); Root.Add(body);
            safeArea = new SafeAreaBinder(body);
            Root.style.display = DisplayStyle.None; host.Add(Root);
            Root.RegisterCallback<GeometryChangedEvent>(_ => { if (open) safeArea.Update(); });
        }

        public void Open(GuildMember member, bool officer)
        {
            body.Clear();
            body.Add(GuildViews.Text("Report player", "mk-h2"));
            body.Add(GuildViews.Text(PlayerCardViews.Handle(member.Player), "mk-h3"));
            body.Add(GuildViews.Text("Choose a reason", "mk-eyebrow"));
            string reason = null;
            var reasons = new VisualElement(); reasons.AddToClassList("lb-guild-report-reasons"); body.Add(reasons);
            var details = new TextField("Details (optional)") { multiline = true, maxLength = 1000 };
            details.AddToClassList("mk-card"); details.AddToClassList("lb-guild-report-details");
            details.Query<TextElement>().ForEach(element => element.enableRichText = false);
            var remove = new Toggle("Also remove from guild") { value = false };
            var actions = new VisualElement(); actions.AddToClassList("lb-guild-report-actions");
            var submit = GuildViews.Button("Submit", () =>
            {
                Debug.Log("[Stub] Report player=" + member.Player.PlayerId + " reason=" + reason + " details=" + details.value + " remove=" + (officer && remove.value));
                Close();
            }, "mk-btn--primary");
            submit.SetEnabled(false);
            foreach (string value in new[] { "Harassment", "Cheating", "Inappropriate name", "Spam", "Other" })
            {
                var chip = new Button { text = value }; chip.AddToClassList("mk-chip"); chip.AddToClassList("lb-guild-reason");
                chip.clicked += () =>
                {
                    reason = value;
                    reasons.Query<Button>().ForEach(button => button.EnableInClassList("mk-chip--on", button == chip));
                    submit.SetEnabled(true);
                };
                reasons.Add(chip);
            }
            body.Add(details);
            if (officer) body.Add(remove);
            actions.Add(GuildViews.Button("Cancel", Close)); actions.Add(submit); body.Add(actions);
            open = true; Root.style.display = DisplayStyle.Flex; Root.BringToFront(); safeArea.Update();
            router.Open(new LobbyOverlay("guild-report", OverlayKind.Fullscreen, true, true), Close);
        }

        public void Close()
        {
            if (!open) return;
            open = false; Root.style.display = DisplayStyle.None; body.Clear(); router.Closed("guild-report");
        }
    }
}
