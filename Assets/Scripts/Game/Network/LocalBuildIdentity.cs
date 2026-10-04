using UnityEngine;
using NodeWar.Config;
using NodeWar.Simulation;

namespace NodeWar.Network
{
    /// <summary>
    /// This build's <see cref="BuildIdentity"/>: the wire protocol, the
    /// simulation version and a hash of the balance it will play with. Sent in
    /// the handshake, and compared against the peer's with
    /// <see cref="InputSerializer.Compare"/>.
    /// </summary>
    public static class LocalBuildIdentity
    {
        private static bool resolved;
        private static BuildIdentity current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            resolved = false;
        }

        public static BuildIdentity Current
        {
            get
            {
                if (!resolved)
                {
                    current = new BuildIdentity(
                        InputSerializer.ProtocolVersion,
                        (ushort)SimulationVersion.Current,
                        ContentHashOf(GameBalance.LoadShared()));
                    resolved = true;
                }
                return current;
            }
        }

        /// <summary>
        /// The content hash of a balance asset. A missing asset hashes to 0,
        /// which no real balance produces in practice, so two builds that both
        /// lost it still refuse anyone who has it.
        /// </summary>
        public static int ContentHashOf(GameBalance balance)
        {
            if (balance == null)
            {
                Debug.LogError("[LocalBuildIdentity] Shared GameBalance not found under Resources/"
                    + GameBalance.SharedResourceName + ".");
                return 0;
            }
            return BalanceHasher.Hash(balance.Data);
        }
    }
}
