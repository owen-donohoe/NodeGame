using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// "Configure controls": a full-height overlay over the Settings page, opened by the
    /// one button that replaced the old Controls section. Rows are grouped by gesture
    /// kind and built here from <see cref="ControlsViewModel"/>; the skeleton (the page,
    /// its back button and the scroll) is authored in SettingsPage.uxml.
    ///
    /// The panel holds no settings. It reads the page's current value through
    /// <c>getCurrent</c> and sends every change back as a view-model edit, so the
    /// page's clone-before-edit, save and Changed paths are the only ones.
    ///
    /// A slot with several actions gets a dropdown. UI Toolkit's DropdownField opens its
    /// list on the panel root, outside this tree, where this page's stylesheet does not
    /// reach, so the list here is a small popup of our own, inside the overlay, in which
    /// the current action is drawn darker.
    /// </summary>
    public sealed class ControlsPanel
    {
        private const float ItemHeight = 56f;
        private const float PopupMinWidth = 240f;

        private sealed class SlotRowView
        {
            public InputSlot slot;
            public VisualElement row;
            public VisualElement check;
            public Button checkHit;
            public Label name;
            public Button action;
            public Label actionText;
            public VisualElement chevron;
        }

        private readonly VisualElement page;
        private readonly ScrollView scroll;
        private readonly VisualElement scrim;
        private readonly VisualElement popup;
        private readonly Func<GameSettingsData> getCurrent;
        private readonly Action<Func<GameSettingsData, GameSettingsData>> edit;
        private readonly Action<float> holdChanged;

        private readonly List<SlotRowView> slotRows = new List<SlotRowView>();
        private VisualElement cameraRow, cameraCheck, zoomRow, zoomCheck, selbarCheck, tooltipsCheck, sideRow;
        private Button zoomHit;
        private Button sideButton;
        private Label sideValue, holdValue, warnings;
        private Slider holdSlider;

        public bool IsOpen { get; private set; }

        /// <summary>False when the layout lacks the overlay, so the page can hide its button.</summary>
        public bool IsWired { get { return page != null && scroll != null && scrim != null && popup != null; } }

        public ControlsPanel(VisualElement root, Func<GameSettingsData> getCurrent,
            Action<Func<GameSettingsData, GameSettingsData>> edit, Action<float> holdChanged)
        {
            this.getCurrent = getCurrent;
            this.edit = edit;
            this.holdChanged = holdChanged;

            page = root.Q<VisualElement>("controls-panel");
            scroll = root.Q<ScrollView>("controls-scroll");
            scrim = root.Q<VisualElement>("controls-popup-scrim");
            popup = root.Q<VisualElement>("controls-popup");
            if (!IsWired) return;

            Button back = root.Q<Button>("controls-back");
            if (back != null) back.clicked += Close;
            scrim.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == scrim) ClosePopup();
            });

            Build();
        }

        public void Open()
        {
            if (!IsWired) return;
            IsOpen = true;
            page.AddToClassList("cp-page--open");
            Render();
            scroll.scrollOffset = Vector2.zero;
        }

        public void Close()
        {
            ClosePopup();
            IsOpen = false;
            if (page != null) page.RemoveFromClassList("cp-page--open");
        }

        // ===== BUILD =====

        private void Build()
        {
            VisualElement content = scroll.contentContainer;

            foreach (ControlsViewModel.ControlsGroup group in ControlsViewModel.Groups)
            {
                VisualElement card = AddGroup(content, group.Title);
                for (int i = 0; i < group.Slots.Count; i++)
                    card.Add(BuildSlotRow(group.Slots[i], i == group.Slots.Count - 1));
            }

            VisualElement layout = AddGroup(content, ControlsViewModel.CameraAndLayoutTitle);

            cameraRow = SwitchRow("Camera button", out cameraCheck, out _,
                () => edit(ControlsViewModel.ToggleCameraButton));
            layout.Add(cameraRow);

            zoomRow = SwitchRow("Drag to zoom", out zoomCheck, out zoomHit,
                () => edit(ControlsViewModel.ToggleCameraButtonZoom));
            zoomRow.AddToClassList("cp-row--sub");
            layout.Add(zoomRow);

            layout.Add(SwitchRow("Selection counter", out selbarCheck, out _,
                () => edit(ControlsViewModel.ToggleSelectionBar)));

            layout.Add(SwitchRow("Tooltips", out tooltipsCheck, out _,
                () => edit(ControlsViewModel.ToggleTooltips)));

            sideRow = NewRow();
            sideRow.Add(Spacer());
            sideRow.Add(NameLabel("Controls side"));
            sideButton = new Button(() => edit(ControlsViewModel.CycleControlsSide));
            sideButton.AddToClassList("ui-reset-button");
            sideButton.AddToClassList("cp-action");
            sideValue = new Label();
            sideValue.AddToClassList("cp-action__text");
            sideValue.AddToClassList("ui-w500");
            sideValue.pickingMode = PickingMode.Ignore;
            sideButton.Add(sideValue);
            sideRow.Add(sideButton);
            layout.Add(sideRow);

            VisualElement holdRow = NewRow();
            holdRow.AddToClassList("cp-row--last");
            holdRow.Add(Spacer());
            holdRow.Add(NameLabel("Hold time"));
            holdSlider = new Slider(0.15f, 1f);
            holdSlider.AddToClassList("lb-slider");
            holdSlider.AddToClassList("cp-slider");
            holdSlider.RegisterValueChangedCallback(evt =>
            {
                if (holdChanged != null) holdChanged(evt.newValue);
                RenderHoldValue(evt.newValue);
                RenderWarnings();
            });
            holdRow.Add(holdSlider);
            holdValue = new Label();
            holdValue.AddToClassList("cp-hold-value");
            holdValue.AddToClassList("ui-w500");
            holdValue.pickingMode = PickingMode.Ignore;
            holdRow.Add(holdValue);
            layout.Add(holdRow);

            warnings = new Label();
            warnings.AddToClassList("cp-warnings");
            warnings.AddToClassList("ui-w500");
            warnings.pickingMode = PickingMode.Ignore;
            content.Add(warnings);

            Button reset = new Button(() => edit(ControlsViewModel.ResetControls));
            reset.AddToClassList("ui-reset-button");
            reset.AddToClassList("cp-reset");
            Label resetText = new Label("Reset controls to default");
            resetText.AddToClassList("ui-w500");
            resetText.pickingMode = PickingMode.Ignore;
            reset.Add(resetText);
            content.Add(reset);
        }

        private static VisualElement AddGroup(VisualElement content, string title)
        {
            Label header = new Label(title);
            header.AddToClassList("cp-header");
            header.AddToClassList("ui-w600");
            header.pickingMode = PickingMode.Ignore;
            content.Add(header);

            VisualElement wrap = new VisualElement();
            wrap.AddToClassList("lb-group-wrap");
            wrap.AddToClassList("ui-sticker");
            VisualElement shadow = new VisualElement();
            shadow.AddToClassList("ui-sticker__shadow");
            shadow.pickingMode = PickingMode.Ignore;
            wrap.Add(shadow);
            VisualElement card = new VisualElement();
            card.AddToClassList("lb-group");
            wrap.Add(card);
            content.Add(wrap);
            return card;
        }

        private static VisualElement NewRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("cp-row");
            return row;
        }

        private static VisualElement Spacer()
        {
            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("cp-checkhit");
            spacer.pickingMode = PickingMode.Ignore;
            return spacer;
        }

        private static Label NameLabel(string text)
        {
            Label name = new Label(text);
            name.AddToClassList("cp-name");
            name.AddToClassList("ui-w500");
            name.pickingMode = PickingMode.Ignore;
            return name;
        }

        /// <summary>The square on the far left: filled when on, an empty outline when off.</summary>
        private static Button NewCheck(out VisualElement square)
        {
            Button hit = new Button();
            hit.AddToClassList("ui-reset-button");
            hit.AddToClassList("cp-checkhit");
            square = new VisualElement();
            square.AddToClassList("cp-check");
            square.pickingMode = PickingMode.Ignore;
            hit.Add(square);
            return hit;
        }

        private VisualElement SwitchRow(string label, out VisualElement square, out Button hit,
            Action toggle)
        {
            VisualElement row = NewRow();
            hit = NewCheck(out square);
            hit.clicked += toggle;
            row.Add(hit);
            row.Add(NameLabel(label));
            return row;
        }

        private VisualElement BuildSlotRow(InputSlot slot, bool last)
        {
            InputSlot captured = slot;
            var view = new SlotRowView { slot = slot };

            view.row = NewRow();
            if (last) view.row.AddToClassList("cp-row--last");

            view.checkHit = NewCheck(out view.check);
            if (InputBindings.DefinitionFor(slot).Locked)
            {
                // Always on, so nothing to flip and nothing to tap.
                view.checkHit.pickingMode = PickingMode.Ignore;
                view.checkHit.AddToClassList("cp-checkhit--locked");
            }
            else view.checkHit.clicked += () => edit(s => ControlsViewModel.ToggleSlot(s, captured));
            view.row.Add(view.checkHit);

            view.name = NameLabel(ControlsViewModel.SlotLabel(slot));
            view.row.Add(view.name);

            view.action = new Button();
            view.action.AddToClassList("ui-reset-button");
            view.action.AddToClassList("cp-action");
            view.actionText = new Label();
            view.actionText.AddToClassList("cp-action__text");
            view.actionText.AddToClassList("ui-w500");
            view.actionText.pickingMode = PickingMode.Ignore;
            view.action.Add(view.actionText);
            view.chevron = new VisualElement();
            view.chevron.AddToClassList("cp-chevron");
            view.chevron.pickingMode = PickingMode.Ignore;
            view.action.Add(view.chevron);
            view.action.clicked += () => OpenPopup(captured, view.action);
            view.row.Add(view.action);

            slotRows.Add(view);
            return view.row;
        }

        // ===== RENDER =====

        /// <summary>Pushes the page's current settings into every row. Raises nothing.</summary>
        public void Render()
        {
            if (!IsWired) return;
            GameSettingsData current = getCurrent();

            foreach (SlotRowView view in slotRows)
            {
                ControlsRow row = ControlsViewModel.RowFor(current, view.slot);
                view.check.EnableInClassList("cp-check--on", row.Enabled);
                view.row.EnableInClassList("cp-row--dim", row.Dimmed);
                view.actionText.text = row.ActionLabel;
                // One allowed action: plain text, no chevron, nothing to tap.
                view.action.EnableInClassList("cp-action--plain", !row.HasDropdown);
                view.action.pickingMode = row.HasDropdown ? PickingMode.Position : PickingMode.Ignore;
                view.chevron.style.display = row.HasDropdown ? DisplayStyle.Flex : DisplayStyle.None;
            }

            cameraCheck.EnableInClassList("cp-check--on", current.showCameraButton);
            zoomCheck.EnableInClassList("cp-check--on", current.cameraButtonZoom);
            selbarCheck.EnableInClassList("cp-check--on", current.showSelectionBar);
            tooltipsCheck.EnableInClassList("cp-check--on", current.tooltips);

            bool zoomDimmed = ControlsViewModel.CameraZoomDimmed(current);
            zoomRow.EnableInClassList("cp-row--dim", zoomDimmed);
            zoomHit.pickingMode = zoomDimmed ? PickingMode.Ignore : PickingMode.Position;

            sideValue.text = ControlsViewModel.SideLabel(current.controlsSide);
            holdSlider.SetValueWithoutNotify(current.holdTime);
            RenderHoldValue(current.holdTime);
            RenderWarnings();
        }

        private void RenderHoldValue(float seconds)
        {
            holdValue.text = ControlsViewModel.HoldTimeLabel(seconds);
        }

        private void RenderWarnings()
        {
            string text = ControlsViewModel.WarningsText(getCurrent());
            warnings.text = text;
            warnings.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ===== POPUP =====

        private void OpenPopup(InputSlot slot, VisualElement anchor)
        {
            ControlsRow row = ControlsViewModel.RowFor(getCurrent(), slot);
            if (!row.HasDropdown) return;

            popup.Clear();
            for (int i = 0; i < row.Options.Count; i++)
            {
                InputAction option = row.Options[i];
                Button item = new Button(() =>
                {
                    ClosePopup();
                    edit(s => ControlsViewModel.SetAction(s, slot, option));
                });
                item.AddToClassList("ui-reset-button");
                item.AddToClassList("cp-item");
                item.EnableInClassList("cp-item--selected", i == row.SelectedIndex);
                Label text = new Label(row.OptionLabels[i]);
                text.AddToClassList("cp-item__text");
                text.AddToClassList("ui-w500");
                text.pickingMode = PickingMode.Ignore;
                item.Add(text);
                popup.Add(item);
            }

            scrim.AddToClassList("cp-scrim--open");

            // Right-aligned under the button; above it when there is no room below. The
            // height is counted, not measured, so it can be placed before first layout.
            Rect bound = anchor.worldBound;
            Vector2 topLeft = scrim.WorldToLocal(bound.position);
            float width = Mathf.Max(bound.width, PopupMinWidth);
            float height = row.Options.Count * ItemHeight + 4f;
            float available = scrim.resolvedStyle.width;
            float left = Mathf.Clamp(topLeft.x + bound.width - width, 8f, Mathf.Max(8f, available - width - 8f));
            float top = topLeft.y + bound.height + 4f;
            float room = scrim.resolvedStyle.height;
            if (top + height > room && topLeft.y - height - 4f >= 0f) top = topLeft.y - height - 4f;
            popup.style.left = left;
            popup.style.top = top;
            popup.style.width = width;
        }

        private void ClosePopup()
        {
            if (scrim != null) scrim.RemoveFromClassList("cp-scrim--open");
        }
    }
}
