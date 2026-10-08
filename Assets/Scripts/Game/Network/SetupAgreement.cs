using NodeWar.Simulation;

namespace NodeWar.Network
{
    /// <summary>
    /// The pre-draft agreement on the map and the rules. The host proposes a
    /// <see cref="MatchSetup"/> and keeps proposing it until acknowledged; the
    /// guest verifies it against the shipped catalog and its own rules, then
    /// acknowledges or refuses. Until a side is <see cref="Agreed"/> it honours
    /// no draft packet (<see cref="AcceptsDraftPackets"/>), so a changed or
    /// unknown board can never be drafted on, only refused.
    ///
    /// Pure data in, bytes out, no clocks and no Unity: the draft's retry timer
    /// calls <see cref="NextToSend"/> and feeds whatever arrives to
    /// <see cref="Receive"/>, and a lossy test link can do the same.
    /// </summary>
    public sealed class SetupAgreement
    {
        private readonly bool isHost;
        private readonly MatchSetup proposal;
        private readonly IBoardCatalog catalog;
        private readonly ushort localSimulationVersion;
        private readonly int localBalanceHash;
        private readonly string expectedMapId;

        private SetupAgreement(bool isHost, MatchSetup proposal, IBoardCatalog catalog,
            ushort sim, int balance, string expectedMapId)
        {
            this.isHost = isHost;
            this.proposal = proposal;
            this.catalog = catalog;
            localSimulationVersion = sim;
            localBalanceHash = balance;
            this.expectedMapId = expectedMapId;
        }

        /// <summary>The host side: proposes <paramref name="proposal"/> until the guest accepts it.</summary>
        public static SetupAgreement Host(MatchSetup proposal)
        {
            if (proposal == null) throw new System.ArgumentNullException(nameof(proposal));
            return new SetupAgreement(true, proposal, null, proposal.SimulationVersion, proposal.BalanceHash, null);
        }

        /// <summary>
        /// The guest side: accepts a proposal only if <paramref name="catalog"/> vouches
        /// for its map and board and it names this build's simulation version and balance.
        /// When the server allocated a map, pass its ID: a different (even valid) map is refused.
        /// </summary>
        public static SetupAgreement Guest(IBoardCatalog catalog, ushort simulationVersion, int balanceHash,
            string expectedMapId = null)
        {
            return new SetupAgreement(false, null, catalog, simulationVersion, balanceHash, expectedMapId);
        }

        public bool Agreed { get; private set; }
        public bool Refused { get; private set; }
        public string Error { get; private set; }

        /// <summary>The setup both sides agreed on. Null until then.</summary>
        public MatchSetup Setup { get; private set; }

        /// <summary>Whether draft packets may be acted on yet: only once agreed.</summary>
        public bool AcceptsDraftPackets
        {
            get { return Agreed; }
        }

        /// <summary>
        /// What the host should (re)send now: its proposal, until it is acknowledged
        /// or refused. Always null for the guest, which only answers.
        /// </summary>
        public byte[] NextToSend()
        {
            if (!isHost || Agreed || Refused) return null;
            return DraftSerializer.SerializeMatchSetup(proposal);
        }

        /// <summary>
        /// Feeds one received packet and returns the reply to send, if any. Packets
        /// that are not setup packets, or do not parse, change nothing.
        /// </summary>
        public byte[] Receive(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return null;
            PacketType type = InputSerializer.ReadPacketType(packet);

            if (!isHost && type == PacketType.MatchSetup &&
                DraftSerializer.TryDeserializeMatchSetup(packet, out MatchSetup proposed))
                return ReceiveProposal(proposed);

            if (isHost && type == PacketType.MatchSetupAck &&
                DraftSerializer.TryDeserializeMatchSetupAck(packet, out MatchSetup acknowledged, out bool accepted))
                ReceiveAck(acknowledged, accepted);

            return null;
        }

        private byte[] ReceiveProposal(MatchSetup proposed)
        {
            if (Refused) return DraftSerializer.SerializeMatchSetupAck(proposed, false);

            // A repeat of what was agreed gets the same answer; anything else is a conflict.
            if (Agreed)
            {
                if (proposed.Equals(Setup)) return DraftSerializer.SerializeMatchSetupAck(proposed, true);
                return Refuse("Conflicting setup after agreement.", proposed);
            }

            string error;
            if (!MatchSetup.Verify(proposed, catalog, localSimulationVersion, localBalanceHash, out error))
                return Refuse(error, proposed);
            if (expectedMapId != null && proposed.MapId != expectedMapId)
                return Refuse("Setup names map \"" + proposed.MapId + "\", not the allocated \"" + expectedMapId + "\".", proposed);

            Agreed = true;
            Setup = proposed;
            return DraftSerializer.SerializeMatchSetupAck(proposed, true);
        }

        private byte[] Refuse(string error, MatchSetup about)
        {
            Refused = true;
            Agreed = false;
            Setup = null;
            Error = error;
            return DraftSerializer.SerializeMatchSetupAck(about, false);
        }

        private void ReceiveAck(MatchSetup acknowledged, bool accepted)
        {
            // Once agreed or refused, late and repeated acks change nothing.
            if (Agreed || Refused) return;

            if (!acknowledged.Equals(proposal))
            {
                Refused = true;
                Error = "The acknowledged setup is not the one proposed.";
            }
            else if (!accepted)
            {
                Refused = true;
                Error = "The opponent refused the setup.";
            }
            else
            {
                Agreed = true;
                Setup = proposal;
            }
        }
    }
}
