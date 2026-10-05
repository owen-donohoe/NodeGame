using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    /// <summary>The actual icon locations, shared by the theme picker and drift diagnostics.</summary>
    public static class LobbyIconUsage
    {
        public readonly struct Location
        {
            public readonly LobbyIconContext Context;
            public readonly string Label;
            public Location(LobbyIconContext context, string label) { Context = context; Label = label; }
        }

        public sealed class Icon
        {
            public LobbyIconKind Kind { get; }
            public string Group { get; }
            public string FriendlyName { get; }
            public string Path => Group + "/" + FriendlyName;
            public bool Retired => Group == "Retired";
            public IReadOnlyList<Location> Locations { get; }
            public Icon(LobbyIconKind kind, string group, string name, params Location[] locations)
            {
                Kind = kind; Group = group; FriendlyName = name;
                Locations = Array.AsReadOnly(locations);
            }
        }

        private static Location At(LobbyIconContext context, string label) => new Location(context, label);
        private static Icon Retired(LobbyIconKind kind) => new Icon(kind, "Retired", kind.ToString());
        public static IReadOnlyList<Icon> All { get; } = Array.AsReadOnly(new[]
        {
            new Icon(LobbyIconKind.None, "Other", "None (choose an icon)"),
            Retired(LobbyIconKind.Shop), Retired(LobbyIconKind.Spark), Retired(LobbyIconKind.Smile),
            Retired(LobbyIconKind.Envelope), Retired(LobbyIconKind.Flag), Retired(LobbyIconKind.Hat),
            Retired(LobbyIconKind.Diamond), Retired(LobbyIconKind.Pip), Retired(LobbyIconKind.Alert), Retired(LobbyIconKind.Capture),
            new Icon(LobbyIconKind.Tools, "Nav bar", "Workshop", At(LobbyIconContext.NavBar, "Workshop navigation button")),
            new Icon(LobbyIconKind.Gear, "Lobby", "Settings gear", At(LobbyIconContext.TopBar, "Lobby top bar")),
            new Icon(LobbyIconKind.Mouth, "Home", "Villager face", At(LobbyIconContext.Home, "Home page villager face")),
            new Icon(LobbyIconKind.Tv, "Lobby", "Match history", At(LobbyIconContext.TopBar, "Lobby top bar"), At(LobbyIconContext.PageHeader, "History page header")),
            new Icon(LobbyIconKind.Back, "Lobby", "Back arrow", At(LobbyIconContext.PageHeader, "Page back buttons"), At(LobbyIconContext.InlineText, "Inline in Settings controls"), At(LobbyIconContext.SuitTree, "Suit tree back button")),
            new Icon(LobbyIconKind.District, "Workshop", "District", At(LobbyIconContext.Workshop, "Workshop tabs and picker")),
            new Icon(LobbyIconKind.Suit, "Workshop", "Suit", At(LobbyIconContext.Workshop, "Workshop tabs and picker")),
            new Icon(LobbyIconKind.Lock, "Workshop", "Locked card", At(LobbyIconContext.Workshop, "Workshop locked card")),
            new Icon(LobbyIconKind.Close, "Node sheet", "Close button", At(LobbyIconContext.NodeSheet, "Node sheet")),
            new Icon(LobbyIconKind.Swords, "Indicators", "Battle", At(LobbyIconContext.OffScreenIndicator, "Battle indicator over the board or screen edge")),
            new Icon(LobbyIconKind.Sleep, "Indicators", "Idle villager", At(LobbyIconContext.OffScreenIndicator, "Idle indicator over the board or screen edge")),
            new Icon(LobbyIconKind.Respawn, "Indicators", "Respawn", At(LobbyIconContext.OffScreenIndicator, "Respawn indicator over the board or screen edge")),
            new Icon(LobbyIconKind.Pointer, "Indicators", "Direction arrow", At(LobbyIconContext.OffScreenIndicator, "Indicator direction arrow")),
            new Icon(LobbyIconKind.Frown, "Emotes", "Sad", At(LobbyIconContext.EmotePicker, "Emote picker"), At(LobbyIconContext.EmoteBubble, "Emote bubble over the board")),
            new Icon(LobbyIconKind.Angry, "Emotes", "Angry", At(LobbyIconContext.EmotePicker, "Emote picker"), At(LobbyIconContext.EmoteBubble, "Emote bubble over the board")),
            new Icon(LobbyIconKind.Speaker, "Emotes", "Mute status", At(LobbyIconContext.HeadsUpDisplay, "HUD and match settings"), At(LobbyIconContext.EmotePicker, "Emote picker")),
            new Icon(LobbyIconKind.Food, "Resources", "Food", At(LobbyIconContext.HeadsUpDisplay, "Heads-up display"), At(LobbyIconContext.NodeSheet, "Node sheet"), At(LobbyIconContext.InlineText, "Inline in sheet text")),
            new Icon(LobbyIconKind.Materials, "Resources", "Materials", At(LobbyIconContext.HeadsUpDisplay, "Heads-up display"), At(LobbyIconContext.NodeSheet, "Node sheet"), At(LobbyIconContext.InlineText, "Inline in sheet text")),
            new Icon(LobbyIconKind.Metal, "Resources", "Metal", At(LobbyIconContext.HeadsUpDisplay, "Heads-up display"), At(LobbyIconContext.NodeSheet, "Node sheet"), At(LobbyIconContext.InlineText, "Inline in sheet text")),
            new Icon(LobbyIconKind.NavBarHome, "Nav bar", "Home", At(LobbyIconContext.NavBar, "Home navigation button")),
            new Icon(LobbyIconKind.NavBarSocial, "Nav bar", "Social", At(LobbyIconContext.NavBar, "Social navigation button")),
            new Icon(LobbyIconKind.NavBarShop, "Nav bar", "Shop", At(LobbyIconContext.NavBar, "Shop navigation button")),
            new Icon(LobbyIconKind.DailyBox, "Home", "Daily box", At(LobbyIconContext.Home, "Home page daily box")),
            new Icon(LobbyIconKind.VictoryBox, "Home", "Victory box", At(LobbyIconContext.Home, "Home page victory box")),
            new Icon(LobbyIconKind.ShopBundle, "Shop", "Bundle", At(LobbyIconContext.ShopCard, "Shop bundle offer")),
            new Icon(LobbyIconKind.GoldLeaf, "Shop", "Gold leaf", At(LobbyIconContext.ShopCard, "Shop gold-leaf offer")),
            new Icon(LobbyIconKind.MagicResource, "Resources", "Magic", At(LobbyIconContext.HeadsUpDisplay, "Heads-up display"), At(LobbyIconContext.NodeSheet, "Node sheet")),
            new Icon(LobbyIconKind.SuitTreeAvailable, "Suit tree", "Available", At(LobbyIconContext.SuitTree, "Available suit-tree node")),
            new Icon(LobbyIconKind.SuitTreeOwned, "Suit tree", "Owned", At(LobbyIconContext.SuitTree, "Owned suit-tree node")),
            new Icon(LobbyIconKind.SuitTreeEquipped, "Suit tree", "Equipped", At(LobbyIconContext.SuitTree, "Equipped suit-tree node")),
            new Icon(LobbyIconKind.SuitTreeLocked, "Suit tree", "Locked", At(LobbyIconContext.SuitTree, "Locked suit-tree node")),
            new Icon(LobbyIconKind.ProfileYouAreHere, "Profile", "You are here", At(LobbyIconContext.Profile, "Profile arena-track marker")),
            new Icon(LobbyIconKind.IndicatorEffect, "Indicators", "Effect", At(LobbyIconContext.OffScreenIndicator, "Effect indicator over the board or screen edge")),
            new Icon(LobbyIconKind.IndicatorThreatToCore, "Indicators", "Threat to core", At(LobbyIconContext.OffScreenIndicator, "Threat-to-core indicator")),
            new Icon(LobbyIconKind.IndicatorThreatToTerritory, "Indicators", "Threat to territory", At(LobbyIconContext.OffScreenIndicator, "Threat-to-territory indicator")),
            new Icon(LobbyIconKind.IndicatorNodeUnderAttack, "Indicators", "Node under attack", At(LobbyIconContext.OffScreenIndicator, "Node-under-attack indicator")),
            new Icon(LobbyIconKind.IndicatorNodeContested, "Indicators", "Contested node", At(LobbyIconContext.OffScreenIndicator, "Contested-node indicator")),
            new Icon(LobbyIconKind.EmoteHappy, "Emotes", "Happy", At(LobbyIconContext.EmotePicker, "Emote picker"), At(LobbyIconContext.EmoteBubble, "Emote bubble over the board")),
            new Icon(LobbyIconKind.EmoteWhiteFlag, "Emotes", "White flag", At(LobbyIconContext.EmotePicker, "Emote picker"), At(LobbyIconContext.EmoteBubble, "Emote bubble over the board")),
            new Icon(LobbyIconKind.CosmeticTinRoof, "Cosmetics", "Tin roof", At(LobbyIconContext.ShopCard, "Shop card and item detail")),
            new Icon(LobbyIconKind.CosmeticPaperBanner, "Cosmetics", "Paper banner", At(LobbyIconContext.ShopCard, "Shop card and item detail")),
            new Icon(LobbyIconKind.CosmeticStrawHat, "Cosmetics", "Straw hat", At(LobbyIconContext.ShopCard, "Shop card and item detail"))
        });

        public static Icon For(LobbyIconKind kind)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Kind == kind) return All[i];
            return null;
        }

        public static bool IsUsed(LobbyIconKind kind, LobbyIconContext context)
        {
            Icon icon = For(kind);
            if (icon == null) return false;
            for (int i = 0; i < icon.Locations.Count; i++)
                if (icon.Locations[i].Context == context) return true;
            return false;
        }

        public static bool IsValidThemePair(LobbyIconKind kind, LobbyIconContext context)
        {
            Icon icon = For(kind);
            return icon != null && icon.Locations.Count > 0 &&
                (context == LobbyIconContext.Anywhere || IsUsed(kind, context));
        }

        public static Location[] WhereChoices(LobbyIconKind kind)
        {
            Icon icon = For(kind);
            int count = icon != null && icon.Locations.Count > 1 ? icon.Locations.Count : 0;
            var choices = new Location[count + 1];
            choices[0] = At(LobbyIconContext.Anywhere, "Anywhere");
            for (int i = 0; i < count; i++) choices[i + 1] = icon.Locations[i];
            return choices;
        }
    }
}
