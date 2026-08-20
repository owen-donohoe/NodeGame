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

    public enum GameMode
    {
        OneVsOne,
        Bot,
        Testing,
        Locked
    }
}