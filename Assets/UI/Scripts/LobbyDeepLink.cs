using System;

namespace NodeWar.Lobby
{
    public enum LobbyRouteKind { Page, ShopStall, Profile, GuildChat, CrateOpen, Bell }

    public readonly struct LobbyRoute
    {
        public LobbyRouteKind Kind { get; }
        public string Target { get; }
        public LobbyRoute(LobbyRouteKind kind, string target = null) { Kind = kind; Target = target; }
    }

    public static class LobbyDeepLink
    {
        public static bool TryParse(string url, out LobbyRoute route)
        {
            route = default;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("nodewar://", StringComparison.OrdinalIgnoreCase)) return false;
            string path = url.Substring(10);
            // This is a small route grammar, not a general web URL. Reject extra components.
            foreach (char c in path)
                if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '?' || c == '#' || c == '%' || c == '\\') return false;
            string[] parts = path.Split('/');
            string head = parts[0].ToLowerInvariant();
            if (parts.Length == 1 && head == "bell") { route = new LobbyRoute(LobbyRouteKind.Bell); return true; }
            if (parts.Length != 2 || parts[1].Length == 0) return false;
            string target = parts[1].ToLowerInvariant();
            if (head == "page" && (target == "shop" || target == "loadout" || target == "home" || target == "social" || target == "guild"))
                route = new LobbyRoute(LobbyRouteKind.Page, target);
            else if (head == "shop" && (target == "daily" || target == "crates" || target == "weekly" || target == "bundle" || target == "coins" || target == "laurels"))
                route = new LobbyRoute(LobbyRouteKind.ShopStall, target);
            else if (head == "profile") route = new LobbyRoute(LobbyRouteKind.Profile, parts[1]);
            else if (head == "guild" && target == "chat") route = new LobbyRoute(LobbyRouteKind.GuildChat);
            else if (head == "crate" && target == "open") route = new LobbyRoute(LobbyRouteKind.CrateOpen);
            else return false;
            return true;
        }
    }
}
