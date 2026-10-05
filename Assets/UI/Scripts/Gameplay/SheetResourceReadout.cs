using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>A persistent resource pill that becomes a thin bar when the sheet uses it.</summary>
    public sealed class SheetResourceReadout
    {
        public VisualElement Root { get; }
        private readonly ResourceKind kind;
        private readonly LobbyIcon icon;
        private readonly Label count;
        private readonly VisualElement barHost;
        private readonly ResourceBar bar;
        private readonly VisualElement tintBackground;
        private Color critical = Color.gray, low = Color.gray, warn = Color.gray;
        private Color ok = Color.gray, good = Color.gray, rich = Color.gray;
        private int value, cap;
        private bool hasValue, expanded;
        private IVisualElementScheduledItem bounceJob, morphJob;

        public SheetResourceReadout(ResourceKind kind, LobbyIconKind glyph)
        {
            this.kind = kind;
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.AddToClassList("sheet__res-readout");
            Root.AddToClassList("hud__res-ring");
            ResourceRingColors.ApplyResourceClass(Root, kind);
            tintBackground = new VisualElement { pickingMode = PickingMode.Ignore };
            tintBackground.AddToClassList("sheet__res-readout-tint");
            Root.Add(tintBackground);
            Root.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                ResourceRingColors.Read(evt.customStyle, ref critical, ref low, ref warn, ref ok, ref good, ref rich);
                Repaint();
            });
            icon = new LobbyIcon(glyph, LobbyIconContext.NodeSheet);
            icon.AddToClassList("sheet__res-chip-icon");
            count = new Label("0") { pickingMode = PickingMode.Ignore };
            count.AddToClassList("sheet__res-chip-value");
            count.AddToClassList("ui-w600");
            barHost = new VisualElement { pickingMode = PickingMode.Ignore };
            barHost.AddToClassList("sheet__res-bar-host");
            bar = new ResourceBar();
            bar.SetResourceKind(kind);
            barHost.Add(bar);
            Root.Add(icon);
            Root.Add(barHost);
            Root.Add(count);
        }

        public void Refresh(int amount, int capacity, bool asBar, bool reduced, bool viewerChanged)
        {
            bool bounce = SheetResourceMath.Bounce(value, amount, hasValue, reduced, viewerChanged);
            bool morph = hasValue && expanded != asBar && !reduced;
            if (reduced || viewerChanged) CancelMotion();
            Root.EnableInClassList("sheet__res-readout--reduced", reduced);
            Root.EnableInClassList("sheet__res-readout--bar", asBar);
            value = amount;
            cap = capacity;
            expanded = asBar;
            hasValue = true;
            count.text = asBar ? amount + "/" + capacity : amount.ToString();
            bar.SetCap(capacity);
            bar.SetReducedMotion(reduced);
            bar.SetValue(amount);
            Repaint();
            if (bounce) Bounce();
            if (morph) Morph();
        }

        private void Repaint()
        {
            Color tint = ResourceRingColors.BaseColorFor(kind, value, cap, critical, low, warn, ok, good, rich);
            icon.SetResourceTint(tint);
            count.style.color = tint;
            tintBackground.style.backgroundColor = new Color(tint.r, tint.g, tint.b, 1f);
            Root.style.borderLeftColor = Root.style.borderRightColor = tint;
            Root.style.borderTopColor = Root.style.borderBottomColor = tint;
        }

        private void Bounce()
        {
            bounceJob?.Pause();
            // Scale directly for a repeat change within the settle window, then ease home.
            Root.style.scale = new Scale(new Vector3(1.06f, 1.12f, 1f));
            bounceJob = Root.schedule.Execute(() =>
            {
                Root.style.scale = new Scale(Vector3.one);
                bounceJob = null;
            }).StartingIn(110);
        }

        private void Morph()
        {
            morphJob?.Pause();
            Root.AddToClassList("sheet__res-readout--changing");
            morphJob = Root.schedule.Execute(() =>
            {
                Root.RemoveFromClassList("sheet__res-readout--changing");
                morphJob = null;
            }).StartingIn(16);
        }

        private void CancelMotion()
        {
            bounceJob?.Pause();
            morphJob?.Pause();
            bounceJob = morphJob = null;
            Root.RemoveFromClassList("sheet__res-readout--changing");
            Root.style.scale = new Scale(Vector3.one);
        }
    }
}
