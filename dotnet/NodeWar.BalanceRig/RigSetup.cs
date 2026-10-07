using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NodeWar.Simulation;

namespace NodeWar.BalanceRig
{
    /// <summary>Everything a match needs that does not change between matches.</summary>
    public sealed class RigSetup
    {
        public GameBalanceData balance;
        public int balanceHash;
        public int sourceBalanceHash;
        public string balanceSource;

        /// <summary>Balance fields copied from GameBalanceData.Default() by --v2-overlay.</summary>
        public string[] overlaidFields = new string[0];

        public BoardConfigData board;
        public string boardSource;

        /// <summary>The board's base draft pool per player (Farm, Mine, Village on the shipped board).</summary>
        public DistrictType[][] baseDraft = new DistrictType[2][];

        /// <summary>Districts each player brings on top of the base pool, like a lobby loadout.</summary>
        public DistrictType[] loadoutNodes = new DistrictType[0];
        public PlayerSetup[] playerSetups;
        public Func<int, int, (int x, int z)> mirror;
    }

    /// <summary>
    /// Loads the shipped board and balance without Unity.
    ///
    /// Balance: the signed-content-hash JSON the Cloud Code referee embeds
    /// (dotnet/NodeWarCloud/NodeWarCloud/Balances), deserialised the way
    /// BalanceCatalog does and checked against its filename, so the rig plays
    /// the numbers the referee would accept.
    ///
    /// Board: a referee takes its board from the match log. A rig has no log,
    /// so it reads the same fields from the BoardConfig .asset's YAML text,
    /// read-only. The asset is the one Unity loads.
    /// </summary>
    public static class RigSetupLoader
    {
        /// <summary>The v2 export (breach bar, tempo, sudden death), the newest in Balances/ by commit.</summary>
        public const string DefaultBalanceFile = "1832066265.json";

        // Unity rewrites these keys only on the asset's next save, so both spellings are read:
        // defaultEdgeWeight and baseDraftNodesP0/P1 are the legacy ones.
        private const string LegacyLinkWeightKey = "defaultEdgeWeight";
        private const string BoardAssetPath = "Assets/Data/Game/Board/DefaultBoardConfig.asset";
        private const string BalancesDir = "dotnet/NodeWarCloud/NodeWarCloud/Balances";

        public static string FindRepoRoot()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, BoardAssetPath)))
                        return dir.FullName;
            }
            throw new FileNotFoundException("Could not find the repository root (looked for " + BoardAssetPath + ").");
        }

        public static RigSetup Load(string balancePath, string boardPath, string loadout, bool v2Overlay = false)
        {
            string root = FindRepoRoot();
            var setup = new RigSetup();

            setup.balanceSource = Path.GetFullPath(balancePath ?? Path.Combine(root, BalancesDir, DefaultBalanceFile));
            // The filename is checked against the source JSON, overlay or not.
            setup.balance = LoadBalance(setup.balanceSource, out setup.sourceBalanceHash);
            if (v2Overlay) setup.balance = ApplyV2Overlay(setup.balance, out setup.overlaidFields);
            setup.balanceHash = BalanceHasher.Hash(setup.balance);

            setup.boardSource = Path.GetFullPath(boardPath ?? Path.Combine(root, BoardAssetPath));
            LoadBoard(setup.boardSource, setup);

            // The shipped board's cores sit at opposite corners of the grid, so
            // its symmetry is the half-turn. A board of another shape supplies its own.
            int cols = setup.board.gridCols, rows = setup.board.gridRows;
            setup.mirror = (x, z) => (cols - 1 - x, rows - 1 - z);

            setup.loadoutNodes = ParseLoadout(loadout);
            return setup;
        }

        public static GameBalanceData LoadBalance(string path, out int hash)
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                TypeNameHandling = TypeNameHandling.None
            });
            using (var text = new StreamReader(path))
            using (var json = new JsonTextReader(text))
            {
                GameBalanceData balance = serializer.Deserialize<GameBalanceData>(json);
                hash = BalanceHasher.Hash(balance);

                string name = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int named)
                    && named != hash)
                    throw new FormatException("Balance hash " + hash + " does not match filename " + name + ".");
                return balance;
            }
        }

        /// <summary>
        /// Fills the v2 breach/tempo/sudden-death fields from
        /// GameBalanceData.Default() wherever an exported balance lacks them.
        /// The exports predate v2, so an absent field deserialises to null/0,
        /// which the simulation reads as "feature off". Returns the same
        /// object; its hash is the effective balance, not the source file's.
        /// </summary>
        public static GameBalanceData ApplyV2Overlay(GameBalanceData balance, out string[] applied)
        {
            GameBalanceData d = GameBalanceData.Default();
            var names = new List<string>();

            if (balance.tempoStageTicks == null) { balance.tempoStageTicks = d.tempoStageTicks; names.Add("tempoStageTicks"); }
            if (balance.tempoClaimPercent == null) { balance.tempoClaimPercent = d.tempoClaimPercent; names.Add("tempoClaimPercent"); }
            if (balance.tempoRespawnPercent == null) { balance.tempoRespawnPercent = d.tempoRespawnPercent; names.Add("tempoRespawnPercent"); }
            if (balance.tempoProductionPercent == null) { balance.tempoProductionPercent = d.tempoProductionPercent; names.Add("tempoProductionPercent"); }
            if (balance.suddenDeathTicks == null) { balance.suddenDeathTicks = d.suddenDeathTicks; names.Add("suddenDeathTicks"); }
            if (balance.suddenDeathThresholds == null) { balance.suddenDeathThresholds = d.suddenDeathThresholds; names.Add("suddenDeathThresholds"); }
            if (balance.breachBarMax == 0) { balance.breachBarMax = d.breachBarMax; names.Add("breachBarMax"); }
            if (balance.breachSwarmRate == null) { balance.breachSwarmRate = d.breachSwarmRate; names.Add("breachSwarmRate"); }
            if (balance.breachBarDecayPerTick == 0) { balance.breachBarDecayPerTick = d.breachBarDecayPerTick; names.Add("breachBarDecayPerTick"); }

            applied = names.ToArray();
            return balance;
        }

        /// <summary>"Barracks,Forge", "none", or "" for no loadout districts.</summary>
        public static DistrictType[] ParseLoadout(string loadout)
        {
            var result = new List<DistrictType>();
            if (!string.IsNullOrWhiteSpace(loadout) && !loadout.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string part in loadout.Split(','))
                {
                    if (!Enum.TryParse(part.Trim(), true, out DistrictType type) || type == DistrictType.None)
                        throw new ArgumentException("Unknown district '" + part + "' in --loadout.");
                    result.Add(type);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Reads the BoardConfig asset's <c>data:</c> block and its two base
        /// draft lists. Unity's YAML for this asset is flat key: value lines and
        /// one list of placements, which is all this reads.
        /// </summary>
        public static void LoadBoard(string path, RigSetup setup)
        {
            string[] lines = File.ReadAllLines(path);
            var board = new BoardConfigData();
            var placements = new List<BoardConfigData.InitialDistrictPlacement>();
            var draft = new[] { new List<DistrictType>(), new List<DistrictType>() };

            object boxed = board;
            var fields = typeof(BoardConfigData).GetFields();

            bool inData = false;
            int list = -1; // -1 none, 0/1 = base draft list for that player
            bool inPlacements = false;
            BoardConfigData.InitialDistrictPlacement current = default;
            bool haveCurrent = false;

            foreach (string raw in lines)
            {
                if (raw.Trim().Length == 0) continue;
                int indent = raw.Length - raw.TrimStart().Length;
                string line = raw.Trim();

                if (indent <= 2 && line.StartsWith("- "))
                {
                    // A list item at the key's own indent: the base draft lists.
                    if (list >= 0 && line.StartsWith("- districtType:"))
                        draft[list].Add((DistrictType)int.Parse(line.Substring(line.IndexOf(':') + 1).Trim(), CultureInfo.InvariantCulture));
                    continue;
                }

                if (indent <= 2)
                {
                    if (haveCurrent) { placements.Add(current); haveCurrent = false; }
                    inData = line == "data:";
                    inPlacements = false;
                    list = line.StartsWith("baseDraftNodesP0:") || line.StartsWith("baseDraftDistrictsP0:") ? 0
                        : line.StartsWith("baseDraftNodesP1:") || line.StartsWith("baseDraftDistrictsP1:") ? 1 : -1;
                    continue;
                }

                if (!inData) continue;

                if (line.StartsWith("initialPlacements:")) { inPlacements = true; continue; }

                if (inPlacements)
                {
                    if (line.StartsWith("- "))
                    {
                        if (haveCurrent) placements.Add(current);
                        current = default;
                        haveCurrent = true;
                        line = line.Substring(2);
                    }
                    int colon = line.IndexOf(':');
                    string key = line.Substring(0, colon).Trim();
                    int value = int.Parse(line.Substring(colon + 1).Trim(), CultureInfo.InvariantCulture);
                    switch (key)
                    {
                        case "gridX": current.gridX = value; break;
                        case "gridZ": current.gridZ = value; break;
                        case "districtType": current.districtType = (DistrictType)value; break;
                        case "ownerID": current.ownerID = value; break;
                        case "claimBar": current.claimBar = value; break;
                    }
                    continue;
                }

                int c = line.IndexOf(':');
                if (c < 0) continue;
                string name = line.Substring(0, c).Trim();
                // The asset is Unity's text and keeps the key it was saved with.
                if (name == LegacyLinkWeightKey) name = nameof(BoardConfigData.defaultLinkWeight);
                foreach (var f in fields)
                {
                    if (f.Name == name && f.FieldType == typeof(int))
                        f.SetValue(boxed, int.Parse(line.Substring(c + 1).Trim(), CultureInfo.InvariantCulture));
                }
            }
            if (haveCurrent) placements.Add(current);

            board = (BoardConfigData)boxed;
            board.initialPlacements = placements.ToArray();
            if (board.gridCols <= 0 || board.gridRows <= 0 || placements.Count == 0)
                throw new FormatException("Board asset '" + path + "' did not parse to a board.");

            setup.board = board;
            setup.baseDraft[0] = draft[0].ToArray();
            setup.baseDraft[1] = draft[1].ToArray();
        }
    }
}
