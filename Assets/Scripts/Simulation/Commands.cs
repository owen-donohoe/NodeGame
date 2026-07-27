namespace NodeWar.Simulation
{
    public enum CommandType
    {
        None,
        Move
    }

    [System.Serializable]
    public struct GameCommand
    {
        public CommandType type;
        public int playerID;
        public int villagerID;
        public int targetNodeID;
        public int issuedOnTick;
    }
}