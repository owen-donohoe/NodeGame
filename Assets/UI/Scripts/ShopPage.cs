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

        private static readonly string[] StallNames = { "daily", "crates", "weekly", "bundle", "coins", "laurels" };
        private static readonly string[] Titles = { "Daily", "Crates", "Weekly skins", "Personalised bundle", "Coins", "Laurels" };
        private static readonly string[] Odds =
        {
            "Placeholder odds per reward kind; illustrative only.",
            "Coins: 40%", "Premium (Laurels): 20%", "District: 15%", "Suit: 15%", "Skin: 10%"
        };

        private readonly ScrollView scroll;
        private readonly VisualElement host;
        private readonly VisualElement tail;
        private readonly VisualElement[] stalls = new VisualElement[6];
        private readonly VisualElement[] offers = new VisualElement[6];
        private readonly Label[] countdowns = new Label[6];
        private readonly float[] tops = new float[6];
        private readonly float[] heights = new float[6];
        private readonly DateTime[] resets = new DateTime[6];
        private readonly IVisualElementScheduledItem motion;
        private readonly IVisualElementScheduledItem clock;
        private readonly float deceleration;
        private ClaimPanel claims;
        private BadgeBoard badges;
        private Offer pending;
        private Offer dailyGift;
        private DateTime dailyDate;
        private DateTime bundleStart;
        private bool dailyClaimed;
        private bool dragging;
        private bool needsSnap;
        private bool snapping;
        private float snapTarget;
        private float previousY;
        private float dragStartY;
        private float direction;
        private double previousTime;
        private int pendingStall = -1;
        private bool geometryReady;

        // Keep the shell's construction signature; the shared claim panel is bound after pages register.
        public ShopPage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast, LobbyContextMenu menu)
            : base(LobbyPageID.Shop, Build())
        {
            Root.userData = this;
            scroll = Root.Q<ScrollView>("shop-scroll");
            deceleration = scroll.scrollDecelerationRate;
            host = Root.Q("shop-stalls");
            tail = Root.Q("shop-tail");
            dailyDate = DateTime.Now.Date;
            bundleStart = DateTime.Now;
            for (int i = 0; i < stalls.Length; i++) BuildStall(i);
            BuildOffers();
            RefreshCountdowns();
            scroll.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                dragging = true; snapping = needsSnap = false;
                scroll.scrollDecelerationRate = deceleration;
                direction = 0f; dragStartY = scroll.scrollOffset.y;
            }, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerUpEvent>(evt => EndDrag(), TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerCancelEvent>(evt => EndDrag(), TrickleDown.TrickleDown);
            scroll.RegisterCallback<WheelEvent>(evt =>
            {
                snapping = false; needsSnap = true;
                scroll.scrollDecelerationRate = deceleration;
            }, TrickleDown.TrickleDown);
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(evt => CacheGeometry());
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt => CacheGeometry());
            motion = Root.schedule.Execute(Animate).Every(16);
            motion.Pause();
            clock = Root.schedule.Execute(RefreshCountdowns).Every(1000);
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
            claims.Confirmed += OnConfirmed;
            claims.Cancelled += OnCancelled;
            RefreshDailyBadge();
        }

        private void BuildStall(int index)
        {
            var stall = new VisualElement { name = "stall-" + StallNames[index] };
            stall.AddToClassList("lb-market-stall");
            var keeper = new Label("M") { tooltip = "Marketeer placeholder", pickingMode = PickingMode.Ignore };
            keeper.AddToClassList("lb-marketeer");
            stall.Add(keeper);
            var title = new Label(Titles[index]);
            title.AddToClassList("lb-market-title");
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
                AddOffer(5, ((i + 1) * 100) + " Laurels", "Placeholder price in USD cents. No payment is made.",
                    ClaimAction.Price("USD cents", (i + 1) * 199), LobbyIconKind.Laurel);
        }

        private Offer AddOffer(int stall, string title, string description, ClaimAction action,
            LobbyIconKind icon, string[] disclosure = null, bool freeDaily = false)
        {
            var offer = new Offer { Request = new ClaimRequest(title, description, action, disclosure), FreeDaily = freeDaily };
            var tile = new Button(() => OpenOffer(offer));
            offer.Tile = tile;
            tile.AddToClassList("lb-market-tile");
            var glyph = new LobbyIcon(icon, LobbyIconContext.ShopCard) { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("lb-market-icon");
            tile.Add(glyph);
            tile.Add(new Label(title) { pickingMode = PickingMode.Ignore, enableRichText = false });
            tile.Add(new Label(action.PrimaryLabel()) { pickingMode = PickingMode.Ignore, enableRichText = false });
            offers[stall].Add(tile);
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

        public override void OnShow()
        {
            previousTime = Time.unscaledTimeAsDouble;
            previousY = scroll.scrollOffset.y;
            RefreshCountdowns();
            CacheGeometry();
            motion.Resume();
        }

        public override void OnHide()
        {
            motion.Pause();
            dragging = snapping = needsSnap = false;
            scroll.scrollDecelerationRate = deceleration;
        }

        public void ScrollTo(string stall)
        {
            int index = Array.IndexOf(StallNames, stall?.ToLowerInvariant());
            if (index < 0) return;
            pendingStall = index;
            dragging = snapping = needsSnap = false;
            scroll.scrollDecelerationRate = 0f;
            CacheGeometry();
        }

        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            needsSnap = Mathf.Abs(scroll.scrollOffset.y - dragStartY) > 4f;
        }

        private void CacheGeometry()
        {
            geometryReady = false;
            for (int i = 0; i < stalls.Length; i++)
            {
                tops[i] = host.layout.y + stalls[i].layout.y;
                heights[i] = stalls[i].layout.height;
                if (heights[i] <= 0f || float.IsNaN(heights[i]) || float.IsNaN(tops[i])) return;
            }
            geometryReady = true;
            float padding = Mathf.Max(0f, scroll.contentViewport.layout.height - heights[heights.Length - 1]);
            if (!Mathf.Approximately(tail.resolvedStyle.height, padding)) tail.style.height = padding;
            if (pendingStall < 0 || heights[pendingStall] <= 0f || float.IsNaN(heights[pendingStall])) return;
            // Wait for layout after Show(Shop), including any tail padding needed by the last stall.
            if (scroll.verticalScroller.highValue + 1f < tops[pendingStall]) return;
            scroll.scrollOffset = new Vector2(0f, tops[pendingStall]);
            previousY = scroll.scrollOffset.y;
            pendingStall = -1;
            scroll.scrollDecelerationRate = deceleration;
        }

        private void Animate()
        {
            double now = Time.unscaledTimeAsDouble;
            float delta = (float)(now - previousTime);
            previousTime = now;
            if (delta <= 0f) return;
            if (pendingStall >= 0) CacheGeometry();
            if (!geometryReady) return;
            float y = scroll.scrollOffset.y;
            float velocity = (y - previousY) / delta;
            if (Mathf.Abs(velocity) > 0.1f) direction = Mathf.Sign(velocity);
            previousY = y;
            if (!dragging && needsSnap && !snapping)
            {
                float? target = ShopSnap.Target(y, velocity == 0f ? direction * 0.01f : velocity, tops);
                if (target.HasValue)
                {
                    snapTarget = Mathf.Clamp(target.Value, scroll.verticalScroller.lowValue, scroll.verticalScroller.highValue);
                    scroll.scrollDecelerationRate = 0f;
                    snapping = true; needsSnap = false;
                }
            }
            if (snapping)
            {
                y = Mathf.MoveTowards(y, snapTarget, delta * 1200f);
                scroll.scrollOffset = new Vector2(0f, y);
                if (Mathf.Abs(y - snapTarget) < 0.5f)
                {
                    snapping = false;
                    scroll.scrollDecelerationRate = deceleration;
                }
            }
            for (int i = 0; i < offers.Length; i++)
                offers[i].style.scale = new Scale(Vector3.one * ShopSnap.Shrink(y, tops[i], heights[i]));
        }
    }
}
