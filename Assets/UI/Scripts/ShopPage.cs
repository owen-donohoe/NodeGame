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
        private readonly ShopPanelPose[] poses = new ShopPanelPose[5];
        private readonly bool[] poseReady = new bool[5];
        private readonly IVisualElementScheduledItem motion;
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
        private double previousTime;
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
                poses[i] = new ShopPanelPose(float.NaN, float.NaN, float.NaN, false);
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
            motion = Root.schedule.Execute(Animate).Every(16);
            motion.Pause();
            clock = Root.schedule.Execute(RefreshCountdowns).Every(1000);
            clock.Pause();
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                motion.Pause(); clock.Pause();
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
            var stall = new VisualElement { name = "stall-" + StallNames[index] };
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
            var tile = new Button(() => OpenOffer(offer));
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
            return offer;
        }

        private void OpenOffer(Offer offer)
        {
            if (claims == null || offer.FreeDaily && dailyClaimed) return;
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
            previousTime = Time.unscaledTimeAsDouble;
            RefreshCountdowns();
            clock.Resume();
            motion.Resume();
        }

        public override void OnHide()
        {
            shown = false;
            motion.Pause();
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

        private void Animate()
        {
            if (!shown || stage == null || Root.panel == null || Root.layout.height <= 0f) return;
            double now = Time.unscaledTimeAsDouble;
            walk.Step(Mathf.Min(0.05f, (float)(now - previousTime)));
            previousTime = now;
            stage.SetShopScroll(walk.Position);
            Camera camera = stage.ShopCamera;
            for (int i = 0; i < stalls.Length; i++)
            {
                Vector3 anchor = stage.ShopAnchor(i);
                Vector3 screen = camera.WorldToScreenPoint(anchor);
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
                float desiredY = i == 0 ? 8f : host.layout.height * vertical;
                ShopPanelPose pose = ShopProjection.Project(point.x, point.y,
                    screen.z, Vector3.Distance(camera.transform.position, anchor),
                    stage.ShopReferenceDistance,
                    i, walk.Position, stalls[i].layout.height, vertical, restPoint.y, desiredY);
                ApplyPose(i, pose);
            }
        }

        private void ApplyPose(int index, ShopPanelPose pose)
        {
            VisualElement panel = stalls[index];
            ShopPanelPose previous = poses[index];
            if (!poseReady[index] || pose.Visible != previous.Visible)
                panel.style.visibility = pose.Visible ? Visibility.Visible : Visibility.Hidden;
            poseReady[index] = true;
            if (!pose.Visible)
            {
                poses[index] = new ShopPanelPose(previous.X, previous.Y, previous.Scale, false);
                return;
            }
            if (ShopProjection.Changed(previous.X, pose.X, 0.25f)) panel.style.left = pose.X;
            if (ShopProjection.Changed(previous.Y, pose.Y, 0.25f)) panel.style.top = pose.Y;
            if (ShopProjection.Changed(previous.Scale, pose.Scale, 0.001f))
                panel.style.scale = new Scale(new Vector3(pose.Scale, pose.Scale, 1f));
            // Cache the values actually written, so sub-epsilon motion accumulates.
            poses[index] = new ShopPanelPose(
                ShopProjection.Changed(previous.X, pose.X, 0.25f) ? pose.X : previous.X,
                ShopProjection.Changed(previous.Y, pose.Y, 0.25f) ? pose.Y : previous.Y,
                ShopProjection.Changed(previous.Scale, pose.Scale, 0.001f) ? pose.Scale : previous.Scale,
                pose.Visible);
        }
    }
}
