namespace NodeWar.Lobby
{
    [System.Serializable]
    public struct LoadoutData
    {
        public string suitID0;
        public string suitID1;
        public string suitID2;
        public string nodeID0;
        public string nodeID1;
    }
    public enum NodeCategory
    {
        Generic,    // Available in base draft pool for all players
        Selectable  // Only available when selected in player loadout
    }

    public enum GameMode
    {
        OneVsOne,
        Bot,
        Testing,
        Locked
    }
}