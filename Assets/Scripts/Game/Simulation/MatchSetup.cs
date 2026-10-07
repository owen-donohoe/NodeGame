using System;

namespace NodeWar.Simulation
{
    /// <summary>
    /// The set of maps a build vouches for. The shipped one is
    /// <see cref="PremadeMaps.Catalog"/>; the server checks logs against its own
    /// copy, never against a board or hash a client supplies.
    /// </summary>
    public interface IBoardCatalog
    {
        /// <summary>A fresh copy of the board for a map ID, or false for an unknown ID.</summary>
        bool TryGet(string mapId, out BoardConfigData board);
    }

    /// <summary>
    /// What two peers, a match log and the server compare to agree on the map
    /// and the rules before anything else happens: which map, the fingerprint of
    /// its board, the simulation version and the balance content hash.
    ///
    /// Immutable, with an explicit constructor and getters so it stays C# 9 and
    /// needs no record support. It identifies the match's map from outside
    /// <see cref="SimulationState"/>; the state carries only the board hash.
    /// </summary>
    public sealed class MatchSetup : IEquatable<MatchSetup>
    {
        public string MapId { get; }
        public int BoardHash { get; }
        public ushort SimulationVersion { get; }
        public int BalanceHash { get; }

        public MatchSetup(string mapId, int boardHash, ushort simulationVersion, int balanceHash)
        {
            MapId = mapId ?? "";
            BoardHash = boardHash;
            SimulationVersion = simulationVersion;
            BalanceHash = balanceHash;
        }

        /// <summary>
        /// The setup for a shipped map under this build's simulation version and the
        /// given balance. Throws <see cref="ArgumentException"/> for a map the
        /// catalog does not have.
        /// </summary>
        public static MatchSetup ForShippedMap(string mapId, int balanceHash)
        {
            if (mapId == null || !PremadeMaps.Catalog.TryGet(mapId, out BoardConfigData board))
                throw new ArgumentException("Unknown map \"" + mapId + "\".", nameof(mapId));
            return new MatchSetup(mapId, BoardHasher.Hash(board), (ushort)Simulation.SimulationVersion.Current, balanceHash);
        }

        /// <summary>
        /// Does <paramref name="received"/> describe a map this catalog vouches for,
        /// under this build's rules? The board is checked against the catalog's own
        /// board for that map, so a peer cannot pair a real map ID with a board of
        /// its own.
        /// </summary>
        public static bool Verify(MatchSetup received, IBoardCatalog catalog, ushort localSimulationVersion,
            int localBalanceHash, out string error)
        {
            if (received == null) { error = "No match setup."; return false; }
            if (catalog == null || !catalog.TryGet(received.MapId, out BoardConfigData board))
            {
                error = "Unknown map \"" + received.MapId + "\".";
                return false;
            }
            if (BoardHasher.Hash(board) != received.BoardHash)
            {
                error = "The board for map \"" + received.MapId + "\" does not match the catalog.";
                return false;
            }
            if (received.SimulationVersion != localSimulationVersion)
            {
                error = "Setup is for simulation version " + received.SimulationVersion
                    + "; this build runs " + localSimulationVersion + ".";
                return false;
            }
            if (received.BalanceHash != localBalanceHash)
            {
                error = "Setup is for a different balance (" + received.BalanceHash + " vs " + localBalanceHash + ").";
                return false;
            }
            error = null;
            return true;
        }

        public bool Equals(MatchSetup other)
        {
            return other != null && MapId == other.MapId && BoardHash == other.BoardHash
                && SimulationVersion == other.SimulationVersion && BalanceHash == other.BalanceHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MatchSetup);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (MapId ?? "").Length;
                for (int i = 0; i < MapId.Length; i++) h = h * 31 + MapId[i];
                h = h * 31 + BoardHash;
                h = h * 31 + SimulationVersion;
                return h * 31 + BalanceHash;
            }
        }
    }
}
