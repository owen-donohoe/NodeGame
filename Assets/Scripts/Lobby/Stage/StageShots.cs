namespace NodeWar.Lobby
{
    public enum StageShot { Home, Districts, Suits, Ocean, Market, Queue }

    public static class StageShots
    {
        public static StageShot ForPage(string pageName)
        {
            switch (pageName)
            {
                case "Shop":
                case "Market": return StageShot.Market;
                case "Workshop":
                case "Loadout": return StageShot.Districts;
                case "Searching": return StageShot.Queue;
                case "Social": return StageShot.Ocean;
                case "Guild": return StageShot.Ocean;
                case "Home": return StageShot.Districts;
                default: return StageShot.Home;
            }
        }
    }
}
