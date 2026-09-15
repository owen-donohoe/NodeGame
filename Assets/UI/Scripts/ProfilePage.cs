using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Profile: the stats and arena view that expands in place from the trophy
    /// strip when the player name is pressed. Not a tab page - it covers the
    /// whole lobby, chrome included, and closes back into the strip.
    ///
    /// Real: the username, the trophy count, and Rename, which validates with
    /// PlayerProfile.ValidateUsername and saves through SetUsername.
    ///
    /// Layout only, pending systems (layout, then system, then layout revised):
    ///   - the record is em-dashes; no match result is written anywhere;
    ///   - the arena road is the prototype's four placeholder eras, all in
    ///     <see cref="Eras"/>, so the arena system replaces one table.
    /// Match history lives on its own page, from the TV button.
    /// </summary>
    public class ProfilePage
    {
        private const string Dash = "—";

        private struct Era
        {
            public string Name;
            public string StyleClass;
            public int Min;
            public int Max;
            public int Chips;
            public int ChipsGot;
            public string Foot;

            public Era(string name, string styleClass, int min, int max, int chips, int chipsGot, string foot)
            {
                Name = name;
                StyleClass = styleClass;
                Min = min;
                Max = max;
                Chips = chips;
                ChipsGot = chipsGot;
                Foot = foot;
            }
        }

        // TODO(arenas): placeholder tiers from lobby-prototype.html, top of the
        // road first. No arena system exists and the tier scheme is undecided
        // (docs/ui-migration-inventory.md); this table is what it replaces.
        private static readonly Era[] Eras =
        {
            new Era("Sorcery", "lb-era--sorcery", 2400, 3200, 4, 0, "Ritual circles, leyline nodes, breach effects"),
            new Era("Steam Era", "lb-era--steam", 1600, 2400, 4, 0, "Chain production, mechanised troops"),
            new Era("Bronze Age", "lb-era--bronze", 800, 1600, 4, 0, "Conditional production, first buff districts"),
            new Era("Ancient", "lb-era--ancient", 0, 800, 6, 4, "Base districts. Learn the core loop."),
        };

        // TODO(stats): no match result is recorded, so every value is a dash.
        private static readonly string[] StatNames = { "MATCHES", "WINS", "LOSSES", "WIN RATE", "STREAK", "BEST" };

        // The prototype's collapse and grow take 340ms.
        private const long CollapseMs = 340;

        private readonly LobbySheet sheet;
        private readonly LobbyToast toast;
        private readonly SafeAreaBinder safeArea;

        private readonly Label usernameLabel;
        private readonly Label subtitleLabel;
        private readonly ScrollView road;
        private readonly VisualElement roadInner;
        private readonly VisualElement rail;
        private readonly VisualElement railFill;
        private readonly VisualElement renameContent;
        private readonly TextField nameField;
        private readonly Label nameNote;

        private VisualElement youMarker;
        private VisualElement sourceRect;
        private IVisualElementScheduledItem hideLater;

        /// <summary>The view added to the shell's overlay host.</summary>
        public VisualElement Root { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>Raised after a rename is saved, so the chrome can redraw.</summary>
        public event System.Action Renamed;

        public ProfilePage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast)
        {
            this.sheet = sheet;
            this.toast = toast;

            VisualElement holder = new VisualElement();
            if (layout != null) layout.CloneTree(holder);

            Root = holder.Q<VisualElement>("profile-arena");
            if (Root == null)
            {
                // Same degradation as every page: labelled, never an exception.
                Root = new VisualElement();
                Root.AddToClassList("lb-arena");
                Root.Add(new Label("Profile layout missing - assign ProfilePage.uxml"));
            }
            else
            {
                // The page's stylesheet was attached to the holder by CloneTree;
                // carry it over, since the holder is discarded.
                for (int i = 0; i < holder.styleSheets.count; i++)
                    Root.styleSheets.Add(holder.styleSheets[i]);
            }

            Root.RemoveFromHierarchy();
            Root.style.display = DisplayStyle.None;

            usernameLabel = Root.Q<Label>("profile-username");
            subtitleLabel = Root.Q<Label>("profile-subtitle");
            road = Root.Q<ScrollView>("profile-road");
            roadInner = Root.Q<VisualElement>("profile-road-inner");
            rail = Root.Q<VisualElement>("profile-rail");
            railFill = Root.Q<VisualElement>("profile-rail-fill");

            VisualElement inner = Root.Q<VisualElement>("push-safe-area");
            safeArea = new SafeAreaBinder(inner != null ? inner : Root);

            renameContent = LobbySheet.Lift(holder, "profile-rename");
            nameField = renameContent != null ? renameContent.Q<TextField>("profile-name-field") : null;
            nameNote = renameContent != null ? renameContent.Q<Label>("profile-name-note") : null;

            Bind(Root, "profile-back", Close);
            Bind(Root, "profile-rename", BeginRename);
            Bind(renameContent, "profile-name-save", CommitRename);
            Bind(renameContent, "profile-name-cancel", () => { if (sheet != null) sheet.Close(); });

            // Enter commits, which is what a one-field form should do on a
            // phone keyboard.
            if (nameField != null)
                nameField.RegisterCallback<KeyDownEvent>(OnNameFieldKeyDown, TrickleDown.TrickleDown);

            BuildRecord(Root.Q<ScrollView>("profile-record"));
            BuildRoad();
        }

        private static void Bind(VisualElement scope, string name, System.Action action)
        {
            if (scope == null) return;
            Button button = scope.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        public void Update()
        {
            if (IsOpen) safeArea.Update();
        }

        // ===== OPEN / CLOSE =====

        /// <summary>
        /// Expands from <paramref name="source"/> (the trophy strip): snap the
        /// full-size view onto the source's rect, then let it transition back to
        /// full size on the next frame.
        /// </summary>
        public void Open(VisualElement source)
        {
            if (IsOpen) return;

            IsOpen = true;
            sourceRect = source;
            if (hideLater != null) hideLater.Pause();

            Refresh();

            Root.style.display = DisplayStyle.Flex;
            Root.AddToClassList("lb-arena--instant");
            SnapTo(source);

            Root.schedule.Execute(() =>
            {
                Root.RemoveFromClassList("lb-arena--instant");
                Root.style.translate = new Translate(0, 0);
                Root.style.scale = new Scale(Vector2.one);
                Root.AddToClassList("lb-arena--open");

                // The road opens at the bottom, so you travel upward, and the
                // rail fills once the view has grown.
                Root.schedule.Execute(() =>
                {
                    if (road != null) road.scrollOffset = new Vector2(0, float.MaxValue);
                    PlaceMarkers();
                }).ExecuteLater(CollapseMs);
            }).ExecuteLater(16);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            if (sheet != null && sheet.IsShowing(renameContent)) sheet.Close();

            if (railFill != null) railFill.style.height = Length.Percent(0);

            Root.RemoveFromClassList("lb-arena--open");
            SnapTo(sourceRect);

            hideLater = Root.schedule.Execute(() => Root.style.display = DisplayStyle.None);
            hideLater.ExecuteLater(CollapseMs);
        }

        private void SnapTo(VisualElement source)
        {
            if (source == null || Root.parent == null) return;

            Rect full = Root.parent.worldBound;
            Rect from = source.worldBound;
            if (full.width <= 0f || full.height <= 0f) return;

            Root.style.translate = new Translate(from.x - full.x, from.y - full.y);
            Root.style.scale = new Scale(new Vector2(from.width / full.width, from.height / full.height));
        }

        // ===== CONTENT =====

        private void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            int trophies = profile != null ? profile.Trophies : 0;

            if (usernameLabel != null)
                usernameLabel.text = profile != null ? profile.Username : "player";

            // TODO(arenas): "Ancient · 450 trophies" in the prototype.
            if (subtitleLabel != null)
                subtitleLabel.text = "Arena " + Dash + " · " + trophies + " trophies";
        }

        /// <summary>The record, one stat per row: label above, the value alone in its panel.</summary>
        private static void BuildRecord(ScrollView record)
        {
            if (record == null) return;

            for (int i = 0; i < StatNames.Length; i++)
            {
                Label label = new Label(StatNames[i]);
                label.AddToClassList("lb-stat__label");
                label.AddToClassList("lb-w600");
                record.Add(label);

                VisualElement wrap = new VisualElement();
                wrap.AddToClassList("lb-sticker");
                wrap.AddToClassList("lb-stat__wrap");
                wrap.pickingMode = PickingMode.Ignore;

                VisualElement shadow = new VisualElement();
                shadow.AddToClassList("lb-sticker__shadow");
                shadow.pickingMode = PickingMode.Ignore;

                Label value = new Label(Dash);
                value.AddToClassList("lb-stat__panel");
                value.AddToClassList("lb-w700");

                wrap.Add(shadow);
                wrap.Add(value);
                record.Add(wrap);
            }
        }

        private void BuildRoad()
        {
            if (roadInner == null) return;

            int trophies = PlayerProfile.Instance != null ? PlayerProfile.Instance.Trophies : 0;

            for (int i = 0; i < Eras.Length; i++)
            {
                Era era = Eras[i];

                VisualElement box = new VisualElement();
                box.AddToClassList("lb-era");
                box.AddToClassList(era.StyleClass);
                if (i == Eras.Length - 1) box.AddToClassList("lb-era--last");
                box.pickingMode = PickingMode.Ignore;

                box.Add(MakeLabel(era.Name, "lb-era__name", "lb-w600"));

                bool here = trophies >= era.Min && (trophies < era.Max || i == 0);
                box.Add(MakeLabel(era.Min + " – " + era.Max + (here ? " · you are here" : ""),
                                  "lb-era__range", "lb-w500"));

                VisualElement items = new VisualElement();
                items.AddToClassList("lb-era__items");
                items.pickingMode = PickingMode.Ignore;

                // TODO(art): the prototype's chips carry unlock icons.
                for (int c = 0; c < era.Chips; c++)
                {
                    VisualElement chip = new VisualElement();
                    chip.AddToClassList("lb-chip");
                    if (c < era.ChipsGot) chip.AddToClassList("lb-chip--got");
                    chip.pickingMode = PickingMode.Ignore;
                    items.Add(chip);
                }

                box.Add(items);
                box.Add(MakeLabel(era.Foot, "lb-era__foot", "lb-w500"));

                roadInner.Add(box);
            }
        }

        private static Label MakeLabel(string text, string styleClass, string weightClass)
        {
            Label label = new Label(text);
            label.AddToClassList(styleClass);
            label.AddToClassList(weightClass);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>
        /// The "you" marker and three waypoint nodes, placed along the rail by
        /// fraction of its height from the bottom, and the rail filled to you.
        /// Done after layout, because the rail's height is the road's.
        /// </summary>
        private void PlaceMarkers()
        {
            if (rail == null || roadInner == null) return;

            float railHeight = rail.layout.height;
            if (float.IsNaN(railHeight) || railHeight <= 0f) return;

            float railBottom = rail.layout.yMax;
            int trophies = PlayerProfile.Instance != null ? PlayerProfile.Instance.Trophies : 0;
            int top = Eras[0].Max;
            float progress = top > 0 ? Mathf.Clamp01((float)trophies / top) : 0f;

            if (youMarker == null)
            {
                float[] waypoints = { 0.25f, 0.5f, 0.75f };
                for (int i = 0; i < waypoints.Length; i++)
                {
                    VisualElement node = new VisualElement();
                    node.AddToClassList("lb-road__node");
                    node.pickingMode = PickingMode.Ignore;
                    node.style.top = railBottom - railHeight * waypoints[i] - 11f;
                    roadInner.Add(node);
                }

                youMarker = new VisualElement();
                youMarker.AddToClassList("lb-road__you");
                youMarker.pickingMode = PickingMode.Ignore;
                youMarker.Add(new LobbyIcon(LobbyIconKind.Pip));
                roadInner.Add(youMarker);
            }

            youMarker.style.top = railBottom - railHeight * progress - 15f;
            if (railFill != null) railFill.style.height = Length.Percent(progress * 100f);
        }

        // ===== RENAME =====

        private void BeginRename()
        {
            if (renameContent == null || sheet == null) return;

            PlayerProfile profile = PlayerProfile.Instance;
            if (nameField != null)
                nameField.SetValueWithoutNotify(profile != null ? profile.Username : "");

            ShowNote(null);
            sheet.Open(renameContent);

            // The field was not in a panel a moment ago, so it cannot take
            // focus until layout has run.
            if (nameField != null)
                nameField.schedule.Execute(() => { nameField.Focus(); nameField.SelectAll(); });
        }

        private void CommitRename()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (profile == null)
            {
                ShowNote("No player profile loaded.");
                return;
            }

            // Trimmed before validating: ValidateUsername accepts spaces, so a
            // name of nothing but spaces would otherwise pass and then be
            // impossible to tell from an empty one on screen.
            string candidate = (nameField != null ? nameField.value : "").Trim();

            if (!PlayerProfile.ValidateUsername(candidate))
            {
                ShowNote("1 to 16 characters: letters, numbers, spaces or underscores.");
                return;
            }

            profile.SetUsername(candidate);

            if (sheet != null) sheet.Close();
            Refresh();
            if (toast != null) toast.Show("Name changed");
            if (Renamed != null) Renamed();
        }

        private void OnNameFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;

            CommitRename();
            evt.StopPropagation();
        }

        /// <summary>The note under the field doubles as the error line.</summary>
        private void ShowNote(string error)
        {
            if (nameNote == null) return;

            bool isError = !string.IsNullOrEmpty(error);
            nameNote.text = isError ? error : "Up to 16 characters. Visible to opponents.";
            nameNote.EnableInClassList("lb-sheet__note--error", isError);
        }
    }
}
