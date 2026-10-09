namespace NodeWar.Simulation
{
    public enum CommandType
    {
        None,
        Move,
        SetAllocation,
        Equip,    
        Respawn,
        Recruit = 5,
        SetAutoRecruit = 6,
        UpgradeFortress = 7,
        InstallMinion = 8
    }

    [System.Serializable]
    public struct GameCommand
    {
        public CommandType type;
        public int playerID;
        public int villagerID;
        public int targetNodeID;
        public int issuedOnTick;
        public int value; // NEW: generic int for commands that need a number (e.g., allocation amount)
    }

    public static class CommandTypes
    {
        public static bool IsKnown(CommandType type)
        {
            switch (type)
            {
                case CommandType.None:
                case CommandType.Move:
                case CommandType.SetAllocation:
                case CommandType.Equip:
                case CommandType.Respawn:
                case CommandType.Recruit:
                case CommandType.InstallMinion:
                case CommandType.UpgradeFortress:
                case CommandType.SetAutoRecruit:
                    return true;
                default:
                    return false;
            }
        }
    }
}
