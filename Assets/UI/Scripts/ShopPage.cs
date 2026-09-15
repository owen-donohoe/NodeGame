using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The Shop, after lobby-prototype.html: a stall with three wares beside
    /// it, and a quiet list of bundles below.
    ///
    /// Layout pass. There is no economy - no coins, no gold leaf, no boxes to
    /// open - so the catalogue below is the prototype's illustrative content,
    /// kept in one place for the shop system to replace, and every purchase
    /// says it is not available yet rather than pretending to work.
    ///
    /// Cosmetics and boxes only. Nothing here may change a match: no stat
    /// upgrades, no purchasable districts or suits. A placeholder that models
    /// pay-to-win is the thing that gets copied when the real system lands.
    /// </summary>
    public class ShopPage : LobbyPage
    {
        private struct Ware
        {
            public string Name;
            public string Cost;
            public LobbyIconKind Icon;

            public Ware(string name, string cost, LobbyIconKind icon)
            {
                Name = name;
                Cost = cost;
                Icon = icon;
            }
        }

        private struct Offer
        {
            public string Section;
            public string Title;
            public string Subtitle;
            public string Action;
            public LobbyIconKind Icon;

            public Offer(string section, string title, string subtitle, string action, LobbyIconKind icon)
            {
                Section = section;
                Title = title;
                Subtitle = subtitle;
                Action = action;
                Icon = icon;
            }
        }

        // TODO(shop): illustrative catalogue from the prototype. Replace with
        // the shop system's stock; keep it cosmetic.
        private static readonly Ware[] Wares =
        {
            new Ware("Paper Banner", "200 coins", LobbyIconKind.Flag),
            new Ware("Tin Roof", "350 coins", LobbyIconKind.Shop),
            new Ware("Straw Hat", "150 coins", LobbyIconKind.Hat),
        };

        private static readonly Offer[] Offers =
        {
            new Offer("Bundles", "Three boxes", "Ancient pool only", "30 leaf", LobbyIconKind.Envelope),
            new Offer("Bundles", "Ten boxes", "Ancient pool only", "90 leaf", LobbyIconKind.Envelope),
            new Offer("Gold leaf", "Dissolve duplicates", "4 spare cosmetics → 20 leaf", "Dissolve", LobbyIconKind.Diamond),
        };

        private const string NotYet = "Purchasing arrives in a later update";

        private readonly LobbySheet sheet;
        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;

        private readonly VisualElement itemContent;
        private readonly Label itemTitle;
        private readonly LobbyIcon itemIcon;
        private readonly Button itemBuy;

        public ShopPage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast, LobbyContextMenu menu)
            : base(LobbyPageID.Shop, Build(layout))
        {
            this.sheet = sheet;
            this.toast = toast;
            this.menu = menu;

            itemContent = LobbySheet.Lift(Root, "shop-item");
            if (itemContent != null)
            {
                itemTitle = itemContent.Q<Label>("shop-item-title");
                itemIcon = itemContent.Q<LobbyIcon>("shop-item-icon");
                itemBuy = itemContent.Q<Button>("shop-item-buy");
                if (itemBuy != null) itemBuy.clicked += () => Say(NotYet);
            }

            BuildWares(Root.Q<VisualElement>("shop-wares"));
            BuildOffers(Root.Q<VisualElement>("shop-tail"));
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-shop";
            root.AddToClassList("lb-page-flush");
            root.pickingMode = PickingMode.Ignore;

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                Label note = new Label("Shop layout missing - assign ShopPage.uxml");
                note.AddToClassList("lb-stub__body");
                root.Add(note);
            }

            return root;
        }

        private void Say(string message)
        {
            if (toast != null) toast.Show(message);
        }

        private void BuildWares(VisualElement host)
        {
            if (host == null) return;

            for (int i = 0; i < Wares.Length; i++)
            {
                Ware ware = Wares[i];

                VisualElement wrap = Sticker("lb-ware-wrap");
                if (i == Wares.Length - 1) wrap.AddToClassList("lb-ware-wrap--last");

                Button button = new Button();
                button.AddToClassList("lb-reset-button");
                button.AddToClassList("lb-ware");
                button.Add(new LobbyIcon(ware.Icon));

                VisualElement text = new VisualElement();
                text.pickingMode = PickingMode.Ignore;
                text.Add(MakeLabel(ware.Name, "lb-ware__name", "lb-w600"));
                text.Add(MakeLabel(ware.Cost, "lb-ware__cost", "lb-w500"));
                button.Add(text);

                button.clicked += () => OpenWare(ware);

                wrap.Add(button);
                host.Add(wrap);
            }
        }

        private void BuildOffers(VisualElement host)
        {
            if (host == null) return;

            string section = null;

            for (int i = 0; i < Offers.Length; i++)
            {
                Offer offer = Offers[i];

                if (offer.Section != section)
                {
                    Label head = MakeLabel(offer.Section, "lb-sechead", "lb-w600");
                    if (section != null) head.AddToClassList("lb-sechead--spaced");
                    host.Add(head);
                    section = offer.Section;
                }

                VisualElement wrap = Sticker("lb-row-wrap");

                VisualElement row = new VisualElement();
                row.AddToClassList("lb-row");

                VisualElement thumb = new VisualElement();
                thumb.AddToClassList("lb-rowthumb");
                thumb.pickingMode = PickingMode.Ignore;
                thumb.Add(new LobbyIcon(offer.Icon));
                row.Add(thumb);

                VisualElement text = new VisualElement();
                text.AddToClassList("lb-row__text");
                text.pickingMode = PickingMode.Ignore;
                text.Add(MakeLabel(offer.Title, "lb-row__title", "lb-w600"));
                text.Add(MakeLabel(offer.Subtitle, "lb-row__sub", "lb-w500"));
                row.Add(text);

                Button pill = new Button();
                pill.text = offer.Action;
                pill.AddToClassList("lb-reset-button");
                pill.AddToClassList("lb-pill");
                pill.AddToClassList("lb-w700");
                pill.clicked += () => Say(NotYet);
                row.Add(pill);

                if (menu != null)
                {
                    menu.Attach(row, () => new List<LobbyMenuItem>
                    {
                        new LobbyMenuItem("Preview", () => Say("Previews arrive in a later update")),
                        new LobbyMenuItem("Details", () => Say(offer.Title + ": " + offer.Subtitle)),
                    });
                }

                wrap.Add(row);
                host.Add(wrap);
            }
        }

        private void OpenWare(Ware ware)
        {
            if (sheet == null || itemContent == null)
            {
                Say(NotYet);
                return;
            }

            if (itemTitle != null) itemTitle.text = ware.Name;
            if (itemIcon != null) itemIcon.Kind = ware.Icon;
            if (itemBuy != null) itemBuy.text = "Buy for " + ware.Cost;

            sheet.Open(itemContent);
        }

        private static VisualElement Sticker(string wrapClass)
        {
            VisualElement wrap = new VisualElement();
            wrap.AddToClassList("lb-sticker");
            wrap.AddToClassList(wrapClass);
            wrap.pickingMode = PickingMode.Ignore;

            VisualElement shadow = new VisualElement();
            shadow.AddToClassList("lb-sticker__shadow");
            shadow.pickingMode = PickingMode.Ignore;
            wrap.Add(shadow);

            return wrap;
        }

        private static Label MakeLabel(string text, string styleClass, string weightClass)
        {
            Label label = new Label(text);
            label.AddToClassList(styleClass);
            label.AddToClassList(weightClass);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
    }
}
