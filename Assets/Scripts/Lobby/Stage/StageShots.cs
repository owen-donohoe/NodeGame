namespace NodeWar.Lobby
{
    public enum StageShot { Market, Loadout, Home, Social, Guild }

    public static class StageShots
    {
        public static StageShot ForPage(string pageName)
        {
            switch (pageName)
            {
                case "Shop":
                case "Market": return StageShot.Market;
                case "Workshop":
                case "Loadout": return StageShot.Loadout;
                case "Social": return StageShot.Social;
                case "Guild": return StageShot.Guild;
                default: return StageShot.Home;
            }
        }
    }
}
