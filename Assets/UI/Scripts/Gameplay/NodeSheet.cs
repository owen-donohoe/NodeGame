using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Input;
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// The node panel, as a bottom sheet, after ingame-prototype.html. The UI
    /// Toolkit counterpart to NodePanelManager's sliding panel.
    ///
    /// A SHEET, NOT A TAKEOVER. Settled 2026-09-02: the opponent keeps acting
    /// while this is open, so the board must stay visible behind it. It covers
    /// the bottom of the screen and no more, and a scrim would defeat the point.
    ///
    /// IT DOES NOT DECIDE WHEN TO OPEN. NodePanelManager already owns that -
    /// TapRouter arbitrates taps, DistrictPanelPolicy decides which districts
    /// are worth a sheet, and the camera focus session hangs off the same call.
    /// Duplicating any of that would give the game two things racing to answer
    /// one tap. So the old manager keeps the input path and, when suppressed,
    /// tells this what to show instead of showing it itself.
    ///
    /// Four contents cover it: three for the six functional districts, plus
    /// ProductionContent for the Farm, Mine and Market a sheet now opens for
    /// their own owner. See NodeSheetContent and DistrictPanelPolicy.HasSheet.
    /// </summary>
    public class NodeSheet
    {
        // How far the handle must be pulled down to dismiss. In panel units,
        // which the 390-wide reference makes roughly points on a phone.
        private const float DismissPull = 42f;

        public VisualElement Root { get; private set; }

        private readonly VisualElement sheet;
        private readonly VisualElement thumb;
        private readonly Label thumbLetter;
        private readonly Label districtLabel;
        private readonly Label ownerLabel;
        private readonly VisualElement ownerMark;
        private readonly Label ownerMarkLabel;
        private readonly VisualElement claimStrip;
        private readonly VisualElement claimP0;
        private readonly VisualElement claimP1;
        private readonly Label claimLabel;
        private readonly Label claimNote;
        private readonly VisualElement contentHost;
        private readonly VisualElement actionHost;
        private readonly ScrollView body;
        private readonly SafeAreaBinder bottomInset;

        private readonly ForgeContent forge = new ForgeContent();
        private readonly CoreContent core = new CoreContent();
        private readonly EquipContent equip = new EquipContent();
        private readonly ProductionContent production = new ProductionContent();

        // One chip per resource, pinned above the sheet's own top edge (see
        // NodeSheet.uss) so they ride its slide and resize for free rather
        // than being repositioned here every frame.
        private readonly ResourceChip foodChip = new ResourceChip(LobbyIconKind.Food);
        private readonly ResourceChip materialsChip = new ResourceChip(LobbyIconKind.Materials);
        private readonly ResourceChip metalChip = new ResourceChip(LobbyIconKind.Metal);

        private SimulationState state;
        private InputBuffer input;
        private NodeWar.Core.ITickProvider ticks;
        private GameBalanceData balance;

        private NodeSheetContent current;
        private int nodeID = -1;
        private string thumbTint;

        // What the header is currently showing. RefreshHeader runs every frame
        // while the sheet is open - the node can change hands under it - but
        // the text only has to be rebuilt when one of these moves.
        private int shownNode = -1;
        private int shownOwner = -2;
        private int shownViewer = -2;
        private int shownClaim = int.MinValue;

        private int dragPointer = -1;
        private float dragStartY;

        public bool IsOpen { get { return nodeID >= 0; } }

        /// <summary>Raised when the player closes the sheet from its own button or handle.</summary>
        public event System.Action Closed;

        public NodeSheet(VisualTreeAsset layout)
        {
            // The host spans the screen so the sheet can pin itself to the
            // bottom edge, and never picks, so the board above stays live.
            Root = new VisualElement();
            Root.name = "node-sheet-host";
            Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;

            if (layout != null) layout.CloneTree(Root);

            // The cloned TemplateContainer must not pick either.
            for (int i = 0; i < Root.childCount; i++)
            {
                Root[i].pickingMode = PickingMode.Ignore;
                Root[i].style.flexGrow = 1;
            }

            sheet = Root.Q<VisualElement>("node-sheet");
            thumb = Root.Q<VisualElement>("sheet-thumb");
            thumbLetter = Root.Q<Label>("sheet-thumb-letter");
            districtLabel = Root.Q<Label>("sheet-district");
            ownerLabel = Root.Q<Label>("sheet-owner");
            ownerMark = Root.Q<VisualElement>("sheet-owner-mark");
            ownerMarkLabel = Root.Q<Label>("sheet-owner-mark-label");
            claimStrip = Root.Q<VisualElement>("sheet-claim");
            claimP0 = Root.Q<VisualElement>("sheet-claim-p0");
            claimP1 = Root.Q<VisualElement>("sheet-claim-p1");
            claimLabel = Root.Q<Label>("sheet-claim-label");
            claimNote = Root.Q<Label>("sheet-claim-note");
            contentHost = Root.Q<VisualElement>("sheet-content");
            actionHost = Root.Q<VisualElement>("sheet-actions");
            body = Root.Q<ScrollView>("sheet-body");

            VisualElement safeBottom = Root.Q<VisualElement>("sheet-safe-bottom");
            if (safeBottom != null) bottomInset = new SafeAreaBinder(safeBottom, SafeAreaBinder.Edges.Bottom);

            Button close = Root.Q<Button>("sheet-close");
            if (close != null)
            {
                VisualElement ring = new VisualElement();
                ring.AddToClassList("sheet__close-ring");
                ring.pickingMode = PickingMode.Ignore;
                ring.Add(new LobbyIcon(LobbyIconKind.Close));
                close.Add(ring);
                close.clicked += CloseByPlayer;
            }

            VisualElement grab = Root.Q<VisualElement>("sheet-grab");
            if (grab != null)
            {
                grab.RegisterCallback<PointerDownEvent>(OnGrabDown);
                grab.RegisterCallback<PointerMoveEvent>(OnGrabMove);
                grab.RegisterCallback<PointerUpEvent>(OnGrabUp);
            }

            // A child of the sheet itself, not of Root: riding the same
            // translate and height transitions the sheet already has means
            // this never needs its own per-frame position math.
            if (sheet != null)
            {
                VisualElement chips = new VisualElement();
                chips.name = "sheet-res-chips";
                chips.AddToClassList("sheet__res-chips");
                chips.pickingMode = PickingMode.Ignore;
                chips.Add(foodChip.Root);
                chips.Add(materialsChip.Root);
                chips.Add(metalChip.Root);
                sheet.Add(chips);
            }

            Close();
        }

        public void Bind(SimulationState simulationState, InputBuffer inputBuffer,
                         NodeWar.Core.ITickProvider tickProvider, GameBalanceData balanceData)
        {
            state = simulationState;
            input = inputBuffer;
            ticks = tickProvider;
            balance = balanceData;
        }

        /// <summary>Keeps the action bar clear of the home indicator. Per frame.</summary>
        public void UpdateSafeArea()
        {
            if (bottomInset != null) bottomInset.Update();
        }

        /// <summary>
        /// Shows the sheet for a node. Called by the controller when
        /// NodePanelManager decides a panel should open, so every guard that
        /// manager applies - unclaimed nodes, informational districts - has
        /// already run.
        /// </summary>
        public void Open(int node, int controlledPID)
        {
            if (state == null) return;
            if (node < 0 || node >= state.nodes.Length) return;

            NodeSheetContent next = ContentFor(state.nodes[node].districtType);

            if (next == null)
            {
                // DistrictPanelPolicy should have stopped this, so reaching here
                // means the two disagree. Closing is the safe answer; an empty
                // sheet would say a district has actions when it has none.
                Close();
                return;
            }

            nodeID = node;
            shownNode = -1;
            shownOwner = -2;
            shownViewer = -2;
            shownClaim = int.MinValue;

            if (current != next)
            {
                actionHost.Clear();
                contentHost.Clear();
                contentHost.Add(next.Root);
                current = next;
            }

            current.Bind(state, input, ticks, balance, node, controlledPID, actionHost);

            if (body != null) body.scrollOffset = Vector2.zero;
            if (sheet != null)
            {
                sheet.AddToClassList("ui-sheet--open");
                sheet.EnableInClassList("sheet--tall", current.Tall);
                sheet.EnableInClassList("sheet--compact", current.Compact);
            }

            RefreshHeader(controlledPID);
            RefreshResourceChips(controlledPID);
            actionHost.EnableInClassList("sheet__actions--empty", !HasVisibleChild(actionHost));
        }

        public void Close()
        {
            nodeID = -1;
            if (sheet != null) sheet.RemoveFromClassList("ui-sheet--open");
        }

        private void CloseByPlayer()
        {
            Close();
            if (Closed != null) Closed();
        }

        /// <summary>
        /// Per-frame while open. The simulation moves under this at 10Hz and
        /// nothing announces a change, so the sheet re-reads rather than
        /// subscribing.
        /// </summary>
        public void Update(int controlledPID)
        {
            if (!IsOpen || state == null) return;

            // The node can change hands while the sheet is open, and a captured
            // district is a different panel - not a redecorated one.
            RefreshHeader(controlledPID);

            if (current != null)
            {
                current.SetViewer(controlledPID);
                current.Refresh();
            }

            RefreshResourceChips(controlledPID);
            actionHost.EnableInClassList("sheet__actions--empty", !HasVisibleChild(actionHost));
        }

        /// <summary>
        /// Live amount and emphasis for each resource chip. Emphasis comes
        /// from the open content, not a district lookup here - see
        /// NodeSheetContent.InvolvedResources.
        /// </summary>
        private void RefreshResourceChips(int controlledPID)
        {
            if (current == null) return;

            PlayerData player = state.players[controlledPID];
            ResourceKind involved = current.InvolvedResources;

            foodChip.Refresh(player.food, (involved & ResourceKind.Food) != 0);
            materialsChip.Refresh(player.materials, (involved & ResourceKind.Materials) != 0);
            metalChip.Refresh(player.metal, (involved & ResourceKind.Metal) != 0);
        }

        private static bool HasVisibleChild(VisualElement host)
        {
            for (int i = 0; i < host.childCount; i++)
            {
                if (!host[i].ClassListContains("sheet__hidden")) return true;
            }

            return false;
        }

        private NodeSheetContent ContentFor(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Forge:
                    return forge;

                case DistrictType.Core:
                    return core;

                // The four CanEquipSuitAtNode accepts. They differ only in which
                // suits they permit, which EquipContent asks GameBalanceData.
                case DistrictType.Barracks:
                case DistrictType.Camp:
                case DistrictType.Arsenal:
                case DistrictType.Sanctuary:
                    return equip;

                // DistrictPanelPolicy.HasSheet only lets these through for
                // their own owner, so this content never has to draw for an
                // opponent's node.
                case DistrictType.Farm:
                case DistrictType.Mine:
                case DistrictType.Market:
                    return production;

                default:
                    return null;
            }
        }

        private void RefreshHeader(int controlledPID)
        {
            NodeData node = state.nodes[nodeID];

            if (nodeID != shownNode)
            {
                shownNode = nodeID;
                RefreshDistrict(node);
            }

            if (node.ownerID != shownOwner || controlledPID != shownViewer)
            {
                shownOwner = node.ownerID;
                shownViewer = controlledPID;
                RefreshOwner(node, controlledPID);
            }

            if (node.claimBar != shownClaim)
            {
                shownClaim = node.claimBar;
                RefreshClaim(node);
            }
        }

        private void RefreshDistrict(NodeData node)
        {
            string name = node.districtType.ToString();

            if (districtLabel != null) districtLabel.text = name;

            // A monogram tile, tinted from the district's ID the same way the
            // lobby tints its cards, so a district looks alike in both scenes.
            string tint = ItemTint.ClassFor("node_" + name.ToLowerInvariant());
            if (thumb != null && tint != thumbTint)
            {
                if (thumbTint != null) thumb.RemoveFromClassList(thumbTint);
                thumb.AddToClassList(tint);
                thumbTint = tint;
            }

            if (thumbLetter != null) thumbLetter.text = name.Substring(0, 1);
        }

        private void RefreshOwner(NodeData node, int controlledPID)
        {
            if (ownerLabel != null)
            {
                if (node.ownerID < 0) ownerLabel.text = "UNCLAIMED";
                else ownerLabel.text = node.ownerID == controlledPID ? "YOURS" : "THEIRS";
            }

            if (ownerMark == null) return;

            // Colour plus shape, as everywhere else: player 0 is a blue square
            // marked 1, player 1 a red circle marked 2, nobody a grey dash.
            ownerMark.EnableInClassList("ui-mark--p0", node.ownerID == 0);
            ownerMark.EnableInClassList("ui-mark--p1", node.ownerID == 1);
            ownerMark.EnableInClassList("ui-mark--unowned", node.ownerID < 0);

            if (ownerMarkLabel != null)
                ownerMarkLabel.text = node.ownerID < 0 ? "-" : (node.ownerID + 1).ToString();
        }

        /// <summary>
        /// The claim bar, drawn as a tug of war rather than a progress bar.
        ///
        /// claimBar is a signed number: positive is player 0, negative is player
        /// 1, and zero is neutral - the value crosses through zero on its way
        /// from one owner to the other, where ownership is lost. A
        /// one-directional fill would say a node is "80% claimed" without
        /// saying by whom, and would show a node being pulled back toward
        /// neutral as progress.
        ///
        /// Shown only while the node is not fully held: a secure node has
        /// nothing to report, and hiding the strip gives its content the room.
        /// </summary>
        private void RefreshClaim(NodeData node)
        {
            int threshold = balance.claimThreshold;
            int claim = node.claimBar;

            bool secure = threshold <= 0 ||
                          (node.ownerID == 0 && claim >= threshold) ||
                          (node.ownerID == 1 && claim <= -threshold);

            if (claimStrip != null) claimStrip.EnableInClassList("sheet__claim--on", !secure);
            if (sheet != null) sheet.EnableInClassList("sheet--claim", !secure);

            if (secure || claimP0 == null || claimP1 == null) return;

            float toward0 = claim > 0 ? Mathf.Min(1f, claim / (float)threshold) : 0f;
            float toward1 = claim < 0 ? Mathf.Min(1f, -claim / (float)threshold) : 0f;

            claimP0.style.width = Length.Percent(toward0 * 100f);
            claimP1.style.width = Length.Percent(toward1 * 100f);

            if (claimLabel != null)
            {
                if (claim == 0) claimLabel.text = "Neutral";
                else if (claim > 0) claimLabel.text = "Player 1 · " + claim + " / " + threshold;
                else claimLabel.text = "Player 2 · " + (-claim) + " / " + threshold;
            }

            if (claimNote != null)
            {
                string faster = balance.decrementMultiplier > 1
                    ? "Pulling against a lean is " + balance.decrementMultiplier + "x faster. "
                    : "";
                claimNote.text = faster + "With both sides standing on it, a node holds still until the fight ends.";
            }
        }

        // ===== DRAG TO DISMISS =====

        private void OnGrabDown(PointerDownEvent evt)
        {
            dragPointer = evt.pointerId;
            dragStartY = evt.position.y;
            (evt.currentTarget as VisualElement).CapturePointer(evt.pointerId);
        }

        private void OnGrabMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != dragPointer) return;
            if (evt.position.y - dragStartY < DismissPull) return;

            ReleaseGrab(evt.currentTarget as VisualElement, evt.pointerId);
            CloseByPlayer();
        }

        private void OnGrabUp(PointerUpEvent evt)
        {
            if (evt.pointerId != dragPointer) return;
            ReleaseGrab(evt.currentTarget as VisualElement, evt.pointerId);
        }

        private void ReleaseGrab(VisualElement grab, int pointerId)
        {
            dragPointer = -1;
            if (grab != null && grab.HasPointerCapture(pointerId)) grab.ReleasePointer(pointerId);
        }

        /// <summary>
        /// One resource's chip: icon, the player's live amount, and a colour
        /// read the same way ResourceRing reads its own - through the
        /// --ring-critical.. --ring-rich custom properties HUD.uss declares
        /// on the "hud__res-ring" class - so this never invents a colour of
        /// its own. When the open content says this resource is involved it
        /// grows, bolds and bounces once; ResourceRingMath decides the shade
        /// either way.
        /// </summary>
        private class ResourceChip
        {
            private const long BounceMilliseconds = 220;

            public VisualElement Root { get; private set; }

            private readonly LobbyIcon icon;
            private readonly Label valueLabel;

            private Color colorCritical = Color.gray;
            private Color colorLow = Color.gray;
            private Color colorWarn = Color.gray;
            private Color colorOk = Color.gray;
            private Color colorGood = Color.gray;
            private Color colorRich = Color.gray;

            private int shownValue = int.MinValue;
            private bool shownEmphasis;
            private IVisualElementScheduledItem bounceJob;

            public ResourceChip(LobbyIconKind kind)
            {
                Root = new VisualElement();
                Root.pickingMode = PickingMode.Ignore;
                Root.AddToClassList("sheet__res-chip");

                // The class the ring itself carries: custom properties only
                // resolve reliably where a selector matches the element or an
                // ancestor, so this chip has to wear it too, not just inherit
                // the tokens from a distant :root.
                Root.AddToClassList("hud__res-ring");
                Root.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);

                icon = new LobbyIcon(kind);
                icon.AddToClassList("sheet__res-chip-icon");

                valueLabel = new Label("0");
                valueLabel.pickingMode = PickingMode.Ignore;
                valueLabel.AddToClassList("sheet__res-chip-value");
                valueLabel.AddToClassList("ui-w600");

                Root.Add(icon);
                Root.Add(valueLabel);
            }

            private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
            {
                ICustomStyle style = evt.customStyle;

                TryRead(style, "--ring-critical", ref colorCritical);
                TryRead(style, "--ring-low", ref colorLow);
                TryRead(style, "--ring-warn", ref colorWarn);
                TryRead(style, "--ring-ok", ref colorOk);
                TryRead(style, "--ring-good", ref colorGood);
                TryRead(style, "--ring-rich", ref colorRich);

                Repaint();
            }

            private static void TryRead(ICustomStyle style, string name, ref Color target)
            {
                CustomStyleProperty<Color> property = new CustomStyleProperty<Color>(name);
                if (style.TryGetValue(property, out Color value)) target = value;
            }

            public void Refresh(int value, bool emphasis)
            {
                if (value != shownValue)
                {
                    shownValue = value;
                    valueLabel.text = value.ToString();
                    Repaint();
                }

                if (emphasis == shownEmphasis) return;
                shownEmphasis = emphasis;

                Root.EnableInClassList("sheet__res-chip--on", emphasis);
                valueLabel.EnableInClassList("ui-w700", emphasis);
                if (emphasis) Bounce();
            }

            private void Repaint()
            {
                Color tint = BaseColorFor(shownValue);
                icon.style.unityBackgroundImageTintColor = tint;
                valueLabel.style.color = tint;
                Root.style.backgroundColor = new Color(tint.r, tint.g, tint.b, 0.22f);
                Root.style.borderLeftColor = tint;
                Root.style.borderRightColor = tint;
                Root.style.borderTopColor = tint;
                Root.style.borderBottomColor = tint;
            }

            private Color BaseColorFor(int value)
            {
                switch (ResourceRingMath.ColorStopIndex(value))
                {
                    case ResourceRingMath.StopCritical: return colorCritical;
                    case ResourceRingMath.StopLow: return colorLow;
                    case ResourceRingMath.StopWarn: return colorWarn;
                    case ResourceRingMath.StopOk: return colorOk;
                    case ResourceRingMath.StopGood:
                        return Color.Lerp(colorOk, colorGood, ResourceRingMath.GoodBlendFraction(value));
                    default: return colorRich;
                }
            }

            /// <summary>One short overshoot-and-settle on the beat the chip becomes relevant. Same idiom as GameplayHUDController.ResourceReadout.Pop.</summary>
            private void Bounce()
            {
                if (bounceJob != null)
                {
                    bounceJob.Pause();
                    bounceJob = null;
                }

                Root.RemoveFromClassList("sheet__res-chip--bounce");
                Root.AddToClassList("sheet__res-chip--bounce");

                bounceJob = Root.schedule.Execute(() =>
                {
                    Root.RemoveFromClassList("sheet__res-chip--bounce");
                    bounceJob = null;
                }).StartingIn(BounceMilliseconds);
            }
        }
    }
}
