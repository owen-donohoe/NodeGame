using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Button behavior and names stay intact; paper is a non-picking underlay.</summary>
    [UxmlElement]
    public partial class PaperButton : Button
    {
        private readonly PaperCard paper;
        private Label caption;
        private string captionText = "";
        public override string text
        {
            get => captionText;
            set
            {
                captionText = value ?? "";
                if (caption != null) caption.text = captionText;
            }
        }
        public PaperButton() : this(null) { }
        public PaperButton(Action clicked) : base(clicked)
        {
            AddToClassList("ui-paper-button");
            paper = new PaperCard { name = "paper", pickingMode = PickingMode.Ignore };
            paper.AddToClassList("ui-paper__underlay");
            hierarchy.Insert(0, paper);
            caption = new Label(captionText) { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("ui-paper-button__caption");
            hierarchy.Add(caption);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EnableInClassList("mk-btn", PaperCard.IsLobbySurface(this));
                if (string.IsNullOrEmpty(name)) name = "paper-button-" + text + "-" + (parent?.IndexOf(this) ?? 0);
                paper.Seed = name;
            });
            RegisterCallback<PointerDownEvent>(_ => paper.Pressed = true);
            RegisterCallback<PointerUpEvent>(_ => paper.Pressed = false);
            RegisterCallback<PointerCancelEvent>(_ => paper.Pressed = false);
            RegisterCallback<PointerCaptureOutEvent>(_ => paper.Pressed = false);
            RegisterCallback<BlurEvent>(_ => paper.Pressed = false);
        }
    }
}
