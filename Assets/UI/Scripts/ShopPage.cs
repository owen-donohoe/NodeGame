using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Illustrative market stalls. Confirmations log; they never change a wallet.</summary>
    public class ShopPage : LobbyPage
    {
        private sealed class Offer
        {
            public ClaimRequest Request;
            public Button Tile;
            public bool FreeDaily;
        }

        private static readonly string[] StallNames = { "daily", "crates", "weekly", "bundle", "bank" };
        private static readonly string[] Titles = { "Daily", "Crates", "Weekly skins", "Personalised bundle", "Bank" };
        private static readonly string[] TopBands = { "top-bar", "strip-wrap", "guild-chrome-card" };
        private static readonly string[] Odds =
        {
            "Placeholder odds per reward kind; illustrative only.",
            "Coins: 40%", LobbyCurrencyNames.Premium + ": 20%", "District: 15%", "Suit: 15%", "Skin: 10%"
        };

        private readonly VisualElement host;
        private readonly VisualElement[] stalls = new VisualElement[5];
        private readonly VisualElement[] offers = new VisualElement[6];
        private readonly Label[] countdowns = new Label[5];
        private readonly DateTime[] resets = new DateTime[5];
        private readonly float[] panelDistances = new float[5];
        private readonly float[] panelHeights = new float[5];
        private readonly int[] panelOrder = { 0, 1, 2, 3, 4 };
        private bool orderReady;
        private readonly LobbyMotion.Spring[] entrances = new LobbyMotion.Spring[5];
        private readonly float[] elapsed = new float[5], exitElapsed = new float[5], exitAlpha = new float[5], exitScale = new float[5];
        private readonly bool[] showing = new bool[5], exiting = new bool[5];
        private readonly System.Collections.Generic.List<VisualElement>[] cards = new System.Collections.Generic.List<VisualElement>[5];
        private readonly IVisualElementScheduledItem clock;
        private readonly ShopSnap walk = new ShopSnap();
        private LobbyStage stage;
        private ClaimPanel claims;
        private BadgeBoard badges;
        private Offer pending;
        private Offer dailyGift;
        private LobbyBadgeDot dailyBadge;
        private DateTime dailyDate;
        private DateTime bundleStart;
        private bool dailyClaimed;

        private int pointer = -1;
        private Vector2 dragStart;
        private Vector2 dragPrevious;
        private long dragTime;
        private SwipeAxis axis;
        private bool shown;
        // Keep the shell's construction signature; the shared claim panel is bound after pages register.
        public ShopPage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast, LobbyContextMenu menu)
            : base(LobbyPageID.Shop, Build())
        {
            Root.userData = this;
            host = Root.Q("shop-stalls");
            dailyDate = DateTime.Now.Date;
            bundleStart = DateTime.Now;
            for (int i = 0; i < stalls.Length; i++)
            {
                BuildStall(i);
                stalls[i].style.display = DisplayStyle.Flex;
                stalls[i].style.visibility = Visibility.Hidden;
                stalls[i].style.opacity = 0f;
            }
            // Bank contains two shelves, but is a single camera stop.
            offers[5] = new VisualElement();
            offers[5].AddToClassList("lb-market-offers");
            var coinsTitle = new Label("Coins");
            coinsTitle.AddToClassList("lb-market-shelf-title");
            coinsTitle.AddToClassList("ui-w600");
            stalls[4].Insert(2, coinsTitle);
            var laurelsTitle = new Label(LobbyCurrencyNames.Premium);
            laurelsTitle.AddToClassList("lb-market-shelf-title");
            laurelsTitle.AddToClassList("ui-w600");
            stalls[4].Add(laurelsTitle);
            stalls[4].Add(offers[5]);
            BuildOffers();
            // Keep tall offer lists usable inside the chrome-safe viewport.
            for (int i = 0; i < stalls.Length; i++)
            {
                var list = new ScrollView { touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic };
                list.style.minHeight = 0; list.style.flexShrink = 1;
                var children = new System.Collections.Generic.List<VisualElement>(stalls[i].Children());
                for (int c = 2; c < children.Count; c++) list.Add(children[c]);
                stalls[i].Add(list); LobbyScrollbars.Hide(list);
            }
            RefreshCountdowns();
            Root.RegisterCallback<PointerDownEvent>(BeginPointer, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerMoveEvent>(MovePointer, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(EndPointer, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerCancelEvent>(_ => CancelPointer());
            Root.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.target == Root && evt.pointerId == pointer) CancelPointer();
            });
            Root.RegisterCallback<WheelEvent>(evt =>
            {
                if (Mathf.Abs(evt.delta.x) >= Mathf.Abs(evt.delta.y)) return;
                walk.ScrollTo(ShopSnap.Target(walk.Position + Mathf.Sign(evt.delta.y) * 0.01f, evt.delta.y));
                evt.StopPropagation();
            });

            clock = Root.schedule.Execute(RefreshCountdowns).Every(1000);
            clock.Pause();
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                clock.Pause();
                if (claims == null) return;
                claims.Confirmed -= OnConfirmed;
                claims.Cancelled -= OnCancelled;
            });
        }

        private static VisualElement Build()
        {
            var root = new VisualElement { name = "page-shop" };
            root.AddToClassList("lb-page-flush");
            root.AddToClassList("lb-market-page");
            Resources.Load<VisualTreeAsset>("Lobby/ShopPage").CloneTree(root);
            return root;
        }

        public void Bind(ClaimPanel panel, BadgeBoard board)
        {
            if (claims != null) { claims.Confirmed -= OnConfirmed; claims.Cancelled -= OnCancelled; }
            claims = panel;
            badges = board;
            dailyBadge?.RemoveFromHierarchy();
            dailyBadge = new LobbyBadgeDot(board, "shop.daily");
            dailyGift.Tile.Add(dailyBadge);
            claims.Confirmed += OnConfirmed;
            claims.Cancelled += OnCancelled;
            RefreshDailyBadge();
        }

        private void BuildStall(int index)
        {
            var stall = new PaperCard { name = "stall-" + StallNames[index] };
            stall.AddToClassList("lb-market-stall");
            if (index == 4) stall.AddToClassList("lb-market-stall--bank");
            var title = new Label(Titles[index]);
            title.AddToClassList("lb-market-title");
            title.AddToClassList("ui-w700");
            stall.Add(title);
            countdowns[index] = new Label();
            countdowns[index].AddToClassList("lb-market-countdown");
            stall.Add(countdowns[index]);
            var grid = new VisualElement();
            grid.AddToClassList("lb-market-offers");
            stall.Add(grid);
            host.Add(stall);
            stalls[index] = stall;
            cards[index] = new System.Collections.Generic.List<VisualElement>();
            offers[index] = grid;
        }

        private void BuildOffers()
        {
            dailyGift = AddOffer(0, "Daily gift", "A free daily placeholder gift.", ClaimAction.Claim, LobbyIconKind.DailyBox, freeDaily: true);
            AddOffer(0, "Paper banner", "Appearance only.", ClaimAction.Price("Coins", 200), LobbyIconKind.CosmeticPaperBanner);
            AddOffer(0, "Straw hat", "Appearance only.", ClaimAction.Price("Coins", 150), LobbyIconKind.CosmeticStrawHat);
            AddOffer(1, "Small crate", "A Small crate. Placeholder odds are available under i.", ClaimAction.Price("Premium", 50), LobbyIconKind.CrateSmall, Odds);
            AddOffer(1, "Medium crate", "A Medium crate. Placeholder odds are available under i.", ClaimAction.Price("Premium", 100), LobbyIconKind.CrateMedium, Odds);
            AddOffer(2, "Tin roof skin", "A weekly cosmetic placeholder.", ClaimAction.Price("Premium", 75), LobbyIconKind.CosmeticTinRoof);
            AddOffer(2, "Banner skin", "A weekly cosmetic placeholder.", ClaimAction.Price("Premium", 100), LobbyIconKind.CosmeticPaperBanner);
            AddOffer(2, "Hat skin", "A weekly cosmetic placeholder.", ClaimAction.Price("Premium", 125), LobbyIconKind.CosmeticStrawHat);
            AddOffer(3, "Two-day bundle", "An illustrative personalised cosmetics bundle.", ClaimAction.Price("Premium", 250), LobbyIconKind.ShopBundle);
            for (int i = 0; i < 3; i++)
                AddOffer(4, ((i + 1) * 500) + " Coins", "A placeholder currency pack.", ClaimAction.Price("Premium", (i + 1) * 25), LobbyIconKind.Pip);
            if (Application.isMobilePlatform)
                AddOffer(5, "Free ad gift", "Ad reward placeholder. No ad is played in this build.", ClaimAction.Claim, LobbyIconKind.Laurel);
            int paid = Application.isMobilePlatform ? 5 : 6;
            for (int i = 0; i < paid; i++)
                AddOffer(5, ((i + 1) * 100) + " " + LobbyCurrencyNames.Premium, "Placeholder price in USD cents. No payment is made.",
                    ClaimAction.Price("USD cents", (i + 1) * 199), LobbyIconKind.Laurel);
        }

        private Offer AddOffer(int stall, string title, string description, ClaimAction action,
            LobbyIconKind icon, string[] disclosure = null, bool freeDaily = false)
        {
            var offer = new Offer { Request = new ClaimRequest(title, description, action, disclosure), FreeDaily = freeDaily };
            var tile = new PaperButton(() => OpenOffer(offer));
            offer.Tile = tile;
            var sticker = new VisualElement();
            sticker.AddToClassList("ui-sticker");
            sticker.AddToClassList("lb-market-card");
            var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            shadow.AddToClassList("ui-sticker__shadow");
            sticker.Add(shadow);
            sticker.Add(tile);
            tile.AddToClassList("ui-reset-button");
            tile.AddToClassList("lb-market-tile");
            if (freeDaily || title == "Free ad gift")
            {
                tile.AddToClassList("lb-market-tile--free");
                var ribbon = new Label("FREE") { pickingMode = PickingMode.Ignore };
                ribbon.AddToClassList("ui-w700");
                ribbon.AddToClassList("lb-market-ribbon");
                tile.Add(ribbon);
            }
            var glyph = new LobbyIcon(icon, LobbyIconContext.ShopCard) { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("lb-market-icon");
            tile.Add(glyph);
            var name = new Label(title) { pickingMode = PickingMode.Ignore, enableRichText = false };
            name.AddToClassList("ui-w600");
            name.AddToClassList("lb-market-name");
            tile.Add(name);
            var price = new Label(action.PrimaryLabel()) { pickingMode = PickingMode.Ignore, enableRichText = false };
            price.AddToClassList("ui-w700");
            price.AddToClassList("lb-market-price");
            tile.Add(price);
            if (disclosure != null)
            {
                var hint = new Label("Reward odds under i") { pickingMode = PickingMode.Ignore };
                hint.AddToClassList("lb-market-disclosure");
                tile.Add(hint);
            }
            offers[stall].Add(sticker);
            cards[stall == 5 ? 4 : stall].Add(sticker);
            return offer;
        }

        private void OpenOffer(Offer offer)
        {
            if (claims == null || walk.Dragging || walk.Moving || stage?.IsPanning == true || offer.FreeDaily && dailyClaimed) return;
            claims.Open(offer.Request);
            pending = offer;
        }

        private void OnConfirmed()
        {
            if (pending == null) return;
            if (pending.FreeDaily)
            {
                // This is visit-local placeholder claim state, not inventory or wallet state.
                dailyClaimed = true;
                dailyGift.Tile.SetEnabled(false);
                RefreshDailyBadge();
            }
            Debug.Log("[Shop] Confirmed placeholder: " + pending.Request.Title);
            pending = null;
        }

        private void OnCancelled() => pending = null;

        private void RefreshDailyBadge() => badges?.Set("shop.daily", dailyClaimed ? 0 : 1);

        private void RefreshCountdowns()
        {
            DateTime now = DateTime.Now;
            if (dailyDate != now.Date)
            {
                dailyDate = now.Date; dailyClaimed = false;
                dailyGift.Tile.SetEnabled(true);
                RefreshDailyBadge();
            }
            if (now >= bundleStart.AddDays(2)) bundleStart = now;
            for (int i = 0; i < countdowns.Length; i++)
            {
                if (resets[i] <= now) resets[i] = ShopResets.Next(now, (StallKind)i, bundleStart);
                countdowns[i].text = "Refresh in " + ShopCountdown.Format(resets[i] - now);
            }
        }

        public void BindStage(LobbyStage value) => stage = value;

        public override void OnShow()
        {
            shown = true;
            // Never carry projected panels from the last camera pose into a visit.
            for (int i = 0; i < stalls.Length; i++)
            {
                stalls[i].style.display = DisplayStyle.Flex;
                stalls[i].style.visibility = Visibility.Hidden;
                stalls[i].style.opacity = 0f;
                showing[i] = exiting[i] = false;
            }

            RefreshCountdowns();
            clock.Resume();

        }

        public override void OnHide()
        {
            shown = false;

            clock.Pause();
            CancelPointer();
            walk.Stop();
        }

        public void ScrollTo(string stall)
        {
            int index = ShopSnap.StallIndex(stall);
            if (index < 0) return;
            CancelPointer();
            walk.ScrollTo(index);
        }

        private void BeginPointer(PointerDownEvent evt)
        {
            if (evt.button != 0 || !evt.isPrimary) return;
            // The shell can capture a horizontal swipe and consume its release.
            // A fresh primary press must clear that gesture before starting another.
            CancelPointer();
            // An overflowing offer list owns vertical gestures inside its viewport.
            for (var target = evt.target as VisualElement; target != null && target != Root; target = target.parent)
                if (target is ScrollView scroll && scroll.contentContainer.layout.height > scroll.contentViewport.layout.height + 1f)
                    return;
            pointer = evt.pointerId;
            dragStart = dragPrevious = evt.position;
            dragTime = evt.timestamp;
            axis = SwipeAxis.Pending;
        }

        private void MovePointer(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 point = evt.position;
            Vector2 delta = point - dragStart;
            if (axis == SwipeAxis.Pending)
            {
                axis = SwipeDecision.Lock(delta.x, delta.y);
                if (axis == SwipeAxis.Vertical)
                {
                    walk.BeginDrag();
                    Root.CapturePointer(pointer);
                }
            }
            if (axis != SwipeAxis.Vertical) return;
            float pixelsPerStall = Mathf.Max(200f, Root.layout.height * 0.65f);
            walk.Drag(-(point.y - dragPrevious.y) / pixelsPerStall,
                Mathf.Max(0.001f, (evt.timestamp - dragTime) / 1000f));
            dragPrevious = point;
            dragTime = evt.timestamp;
            evt.StopPropagation();
        }

        private void EndPointer(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            bool vertical = axis == SwipeAxis.Vertical;
            if (vertical)
            {
                if (evt.timestamp - dragTime > 100) walk.Drag(0f, 0.1f);
                walk.Release();
            }
            int released = pointer;
            pointer = -1;
            if (Root.HasPointerCapture(released)) Root.ReleasePointer(released);
            if (vertical) evt.StopPropagation();
        }

        private void CancelPointer()
        {
            int released = pointer;
            pointer = -1;
            if (walk.Dragging) walk.Release();
            if (released >= 0 && Root.HasPointerCapture(released)) Root.ReleasePointer(released);
        }

        // Called by the shell before its page/camera tick, never by a UI schedule.
        public void Tick(float seconds)
        {
            if (!shown || stage == null) return;
            walk.Step(seconds);
            stage.SetShopScroll(walk.Position);
            for (int i = 0; i < stalls.Length; i++)
            {
                if (showing[i]) { entrances[i].Step(seconds, LobbyMotion.PopDamping); elapsed[i] += seconds; }
                if (exiting[i]) exitElapsed[i] += seconds;
            }
        }

        // Camera has already consumed the same page spring sample this frame.
        public void Project()
        {
            if (!shown || stage == null || Root.panel == null || Root.layout.height <= 0f) return;
            Camera camera = stage.ShopCamera;
            for (int i = 0; i < stalls.Length; i++)
            {
                Vector3 anchor = stage.ShopAnchor(i);
                Vector3 screen = camera.WorldToScreenPoint(anchor);
                panelDistances[i] = Vector3.Distance(camera.transform.position, anchor);
                float measuredHeight = stalls[i].layout.height;
                if (measuredHeight > 0f) panelHeights[i] = measuredHeight;
                Vector2 point = host.WorldToLocal(RuntimePanelUtils.ScreenToPanel(Root.panel,
                    new Vector2(screen.x, Screen.height - screen.y)));
                // At rest all anchors project to the same point. Offset each panel's
                // vertical origin to its declared screen anchor in the usable page.
                Vector3 restAnchor = stage.ShopAnchor(0);
                Vector3 restScreen = camera.WorldToScreenPoint(restAnchor +
                    Vector3.back * walk.Position * LobbyStage.ShopStallSpacing);
                Vector2 restPoint = host.WorldToLocal(RuntimePanelUtils.ScreenToPanel(Root.panel,
                    new Vector2(restScreen.x, Screen.height - restScreen.y)));
                float vertical = ShopSnap.ScreenAnchor(i);
                // Offset is screen-space and independent of camera distance.
                float desiredY = i == 0 ? 8f : host.layout.height * vertical;
                ShopPanelPose pose = ShopProjection.Project(point.x, point.y,
                    screen.z, panelDistances[i],
                    stage.ShopReferenceDistance,
                    i, walk.Position, panelHeights[i], vertical, restPoint.y, desiredY);
                var safe = SafeBounds();
                // Reserve the pop overshoot as well as the steady depth scale.
                stalls[i].style.maxHeight = Mathf.Max(0f, safe.height / (pose.Scale * 1.02f));
                stalls[i].style.overflow = Overflow.Hidden;
                pose = ShopProjection.Constrain(pose, stalls[i].layout.width * 1.02f, panelHeights[i] * 1.02f,
                    safe.xMin, safe.yMin, safe.xMax, safe.yMax);
                ApplyPose(i, new ShopPanelPose(pose.X, pose.Y, pose.Scale,
                    pose.Visible && !walk.Dragging && !walk.Moving && !stage.IsPanning));
            }
            bool changed = ShopProjection.OrderByDistance(panelDistances, panelOrder);
            if (!orderReady || changed)
            {
                // UI Toolkit draws later siblings on top. Do not reorder unless needed.
                for (int i = 0; i < panelOrder.Length; i++) stalls[panelOrder[i]].BringToFront();
                orderReady = true;
            }
        }

        private Rect SafeBounds()
        {
            var root = Root.panel.visualTree;
            var safe = root.Q("safe-area") ?? root.Q("lobby-root") ?? root;
            Rect bounds = host.WorldToLocal(safe.worldBound);
            var pills = root.Q("tabbar-dock");
            foreach (string name in TopBands)
            {
                var top = root.Q(name);
                if (top != null && top.resolvedStyle.display != DisplayStyle.None)
                    bounds.yMin = Mathf.Max(bounds.yMin, host.WorldToLocal(top.worldBound).yMax);
            }
            if (pills != null && pills.resolvedStyle.display != DisplayStyle.None)
                bounds.yMax = Mathf.Min(bounds.yMax, host.WorldToLocal(pills.worldBound).yMin);
            bounds.xMin += 16; bounds.xMax -= 16; bounds.yMin += 16; bounds.yMax -= 16;
            return bounds;
        }

        private void ApplyPose(int index, ShopPanelPose pose)
        {
            var panel = stalls[index];
            if (pose.Visible && !showing[index])
            {
                showing[index] = true; exiting[index] = false; elapsed[index] = 0;
                entrances[index] = new LobbyMotion.Spring(0f) { Target = 1f };
                panel.style.visibility = Visibility.Visible;
            }
            else if (!pose.Visible && showing[index])
            {
                showing[index] = false; exiting[index] = true; exitElapsed[index] = 0;
                exitAlpha[index] = Mathf.Clamp01(elapsed[index] / LobbyMotion.FadeSeconds);
                exitScale[index] = 0.85f + 0.15f * entrances[index].Position;
            }
            if (!showing[index] && !exiting[index]) return;
            float alpha, size;
            if (exiting[index])
            {
                float t = Mathf.Clamp01(exitElapsed[index] / 0.100f);
                alpha = exitAlpha[index] * (1f - t);
                size = Mathf.Lerp(exitScale[index], 0.95f, LobbyMotion.InCubic(t));
                if (t >= 1f) { exiting[index] = false; panel.style.visibility = Visibility.Hidden; }
            }
            else
            {
                alpha = Mathf.Clamp01(elapsed[index] / LobbyMotion.FadeSeconds);
                size = 0.85f + 0.15f * entrances[index].Position;
                for (int c = 0; c < cards[index].Count; c++)
                {
                    float age = Mathf.Max(0f, elapsed[index] - c * 0.040f);
                    var pop = new LobbyMotion.Spring(0f) { Target = 1f };
                    pop.Step(age, LobbyMotion.PopDamping);
                    cards[index][c].style.opacity = Mathf.Clamp01(age / LobbyMotion.FadeSeconds);
                    cards[index][c].style.translate = new Translate(0f, 12f * (1f - pop.Position));
                    cards[index][c].style.scale = new Scale(Vector3.one * (0.85f + 0.15f * pop.Position));
                }
            }
            panel.style.opacity = alpha;
            panel.style.left = pose.X; panel.style.top = pose.Y;
            panel.style.scale = new Scale(new Vector3(pose.Scale * size, pose.Scale * size, 1f));
        }
    }
}
