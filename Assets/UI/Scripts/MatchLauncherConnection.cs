using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The ranked rendezvous's view of a <see cref="MatchLauncher"/>. The
    /// launcher owns the transport, the handshake and their deadlines; this
    /// only translates, so RankedRendezvous stays free of Unity types and
    /// testable. The owner keeps pumping <see cref="MatchLauncher.Update"/>.
    /// </summary>
    public sealed class MatchLauncherConnection : IRankedConnection
    {
        // D8: the host waits 22 s for the guest; the handshake gets 15 s.
        private const float WaitForOpponentSeconds = 22f;
        private const float HandshakeSeconds = 15f;

        private readonly MatchLauncher launcher;

        public MatchLauncherConnection(MatchLauncher launcher)
        {
            this.launcher = launcher;
        }

        public void Host(string matchId, string[] playerIds)
        {
            launcher.HostRanked(matchId, playerIds, WaitForOpponentSeconds, HandshakeSeconds);
        }

        public void Join(string matchId, string[] playerIds, string joinCode)
        {
            launcher.JoinRanked(matchId, playerIds, joinCode, HandshakeSeconds);
        }

        public RankedConnectionPhase Phase
        {
            get
            {
                switch (launcher.CurrentPhase)
                {
                    case MatchLauncher.Phase.CreatingRoom: return RankedConnectionPhase.CreatingRoom;
                    case MatchLauncher.Phase.WaitingForOpponent: return RankedConnectionPhase.WaitingForOpponent;
                    case MatchLauncher.Phase.Connecting: return RankedConnectionPhase.Connecting;
                    case MatchLauncher.Phase.Connected: return RankedConnectionPhase.Connected;
                    case MatchLauncher.Phase.Failed: return RankedConnectionPhase.Failed;
                    default: return RankedConnectionPhase.Idle;
                }
            }
        }

        /// <summary>Only the host's code, and only once the room exists.</summary>
        public string JoinCode
        {
            get
            {
                return launcher.CurrentPhase == MatchLauncher.Phase.WaitingForOpponent ? launcher.JoinCode : null;
            }
        }

        public string FailureMessage
        {
            get { return launcher.CurrentPhase == MatchLauncher.Phase.Failed ? launcher.FailureMessage ?? "" : ""; }
        }

        public void Cancel()
        {
            launcher.Cancel();
        }
    }
}
