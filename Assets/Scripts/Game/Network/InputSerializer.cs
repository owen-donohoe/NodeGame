using NodeWar.Simulation;
using NodeWar.Core;

namespace NodeWar.Network
{
    public enum PacketType : byte
    {
        Handshake = 0,
        HandshakeAck = 1,
        TickInput = 2,
        Heartbeat = 3,
        DraftReady = 4,
        DraftPlacement = 5,
        DraftLoadout = 6,
        DraftAck = 7,
        Emote = 8,
        HandshakeReject = 9
    }

    /// <summary>
    /// What a build is, for deciding whether two builds may play each other.
    /// See <see cref="InputSerializer.ProtocolVersion"/>,
    /// <see cref="NodeWar.Simulation.SimulationVersion"/> and
    /// <see cref="NodeWar.Simulation.BalanceHasher"/>.
    /// </summary>
    public struct BuildIdentity
    {
        public ushort protocol;
        public ushort sim;
        public int content;

        public BuildIdentity(ushort protocol, ushort sim, int content)
        {
            this.protocol = protocol;
            this.sim = sim;
            this.content = content;
        }
    }

    public enum HandshakeVerdict
    {
        Compatible,
        PeerOlder,
        PeerNewer,

        /// <summary>Same versions, different balance: two builds of the same code with different data.</summary>
        ContentMismatch
    }

    /// <summary>
    /// What goes over the wire each tick for one player.
    /// </summary>
    public struct TickInput
    {
        public int forTick;
        public int stateHash; // non-zero every 50 ticks, 0 otherwise
        public GameCommand[] commands;
    }

    /// <summary>
    /// Converts TickInput to/from byte arrays for UDP transmission.
    /// Packet layout (TickInput):
    ///   [PacketType: 1 byte]
    ///   [forTick: 4 bytes]
    ///   [stateHash: 4 bytes]
    ///   [commandCount: 4 bytes]
    ///   [commands: commandCount * 24 bytes]
    ///     per command: type(4) + playerID(4) + villagerID(4) + targetNodeID(4) + issuedOnTick(4) + value(4)
    /// </summary>
    public static class InputSerializer
    {
        /// <summary>
        /// The wire layout's version. Bump it with any change to any packet's
        /// layout, including GameCommand's (which changes BYTES_PER_COMMAND and
        /// must also land in the same commit as this serializer). Builds from
        /// before the versioned handshake are protocol 0: their handshake is a
        /// single byte.
        /// </summary>
        public const ushort ProtocolVersion = 1;

        // Keep in step with GameCommand; a change here is a ProtocolVersion bump.
        private const int BYTES_PER_COMMAND = 24;
        private const int HEADER_BYTES = 1 + 4 + 4 + 4; // type, forTick, stateHash, commandCount

        public static byte[] Serialize(TickInput input)
        {
            int commandCount = (input.commands != null) ? input.commands.Length : 0;
            int size = 1 + 4 + 4 + 4 + (commandCount * BYTES_PER_COMMAND);
            byte[] data = new byte[size];
            int offset = 0;

            data[offset++] = (byte)PacketType.TickInput;
            WriteInt(data, ref offset, input.forTick);
            WriteInt(data, ref offset, input.stateHash);
            WriteInt(data, ref offset, commandCount);

            for (int i = 0; i < commandCount; i++)
            {
                WriteInt(data, ref offset, (int)input.commands[i].type);
                WriteInt(data, ref offset, input.commands[i].playerID);
                WriteInt(data, ref offset, input.commands[i].villagerID);
                WriteInt(data, ref offset, input.commands[i].targetNodeID);
                WriteInt(data, ref offset, input.commands[i].issuedOnTick);
                WriteInt(data, ref offset, input.commands[i].value);
            }

            return data;
        }

        /// <summary>
        /// Reads a TickInput packet. Returns false, with nothing read past the
        /// header, when the bytes cannot be one: shorter than the header, or a
        /// declared command count that does not match the bytes that arrived.
        ///
        /// The count is checked before anything is allocated or read, so a
        /// truncated or corrupted packet is refused here, as a loss the
        /// transport already has to survive, rather than read past its end or
        /// turned into commands. The length must match exactly, not merely be
        /// enough: Serialize never pads, so any other length means the sender
        /// wrote a different layout - a peer on a build whose GameCommand no
        /// longer matches this one.
        /// </summary>
        public static bool TryDeserialize(byte[] data, out TickInput input)
        {
            input = default;

            if (data == null || data.Length < HEADER_BYTES) return false;

            int offset = 1; // skip PacketType byte
            int forTick = ReadInt(data, ref offset);
            int stateHash = ReadInt(data, ref offset);
            int commandCount = ReadInt(data, ref offset);

            // Divide rather than multiply, so a hostile count cannot overflow
            // its way past the check.
            if (commandCount < 0) return false;
            if (commandCount > (data.Length - HEADER_BYTES) / BYTES_PER_COMMAND) return false;
            if (data.Length != HEADER_BYTES + commandCount * BYTES_PER_COMMAND) return false;

            input.forTick = forTick;
            input.stateHash = stateHash;
            input.commands = new GameCommand[commandCount];
            for (int i = 0; i < commandCount; i++)
            {
                input.commands[i].type = (CommandType)ReadInt(data, ref offset);
                input.commands[i].playerID = ReadInt(data, ref offset);
                input.commands[i].villagerID = ReadInt(data, ref offset);
                input.commands[i].targetNodeID = ReadInt(data, ref offset);
                input.commands[i].issuedOnTick = ReadInt(data, ref offset);
                input.commands[i].value = ReadInt(data, ref offset);
            }

            return true;
        }

        /// <summary>Emote: [type:1][player:1][emote:1][seq:2], little-endian.</summary>
        public static byte[] SerializeEmote(int player, EmoteType emote, ushort sequence)
        {
            return new byte[] { (byte)PacketType.Emote, (byte)player, (byte)emote,
                (byte)sequence, (byte)(sequence >> 8) };
        }

        public static bool TryDeserializeEmote(byte[] data,
            out int player, out EmoteType emote, out ushort sequence)
        {
            player = 0;
            emote = default;
            sequence = 0;
            if (data == null || data.Length != 5 || data[0] != (byte)PacketType.Emote ||
                data[1] > 1 || data[2] > (byte)EmoteType.WhiteFlag) return false;

            player = data[1];
            emote = (EmoteType)data[2];
            sequence = (ushort)(data[3] | (data[4] << 8));
            return true;
        }

        // ===== HANDSHAKE =====
        // Handshake, HandshakeAck and HandshakeReject share one layout, each
        // carrying the sender's own identity:
        //   [type:1][protocol:2][sim:2][content:4] = 9 bytes, little-endian.
        // Bytes 0-2 (type and protocol) never change meaning, so any later build
        // can still tell which protocol a peer speaks, whatever follows. A reject
        // carries no reason code: the receiver compares identities itself, so
        // there is nothing extra to version.

        private const int HANDSHAKE_BYTES = 1 + 2 + 2 + 4;

        public static byte[] SerializeHandshake(BuildIdentity self)
        {
            return SerializeIdentity(PacketType.Handshake, self);
        }

        public static byte[] SerializeHandshakeAck(BuildIdentity self)
        {
            return SerializeIdentity(PacketType.HandshakeAck, self);
        }

        public static byte[] SerializeHandshakeReject(BuildIdentity self)
        {
            return SerializeIdentity(PacketType.HandshakeReject, self);
        }

        /// <summary>
        /// False when the packet is not a readable handshake of this layout. A
        /// one-byte handshake from a protocol-0 build lands here: treat it as
        /// <see cref="HandshakeVerdict.PeerOlder"/>.
        /// </summary>
        public static bool TryDeserializeHandshake(byte[] data, out BuildIdentity peer)
        {
            return TryDeserializeIdentity(data, PacketType.Handshake, out peer);
        }

        public static bool TryDeserializeHandshakeAck(byte[] data, out BuildIdentity peer)
        {
            return TryDeserializeIdentity(data, PacketType.HandshakeAck, out peer);
        }

        public static bool TryDeserializeHandshakeReject(byte[] data, out BuildIdentity peer)
        {
            return TryDeserializeIdentity(data, PacketType.HandshakeReject, out peer);
        }

        /// <summary>
        /// Whether this build may play a peer. Protocol decides first, then the
        /// simulation; only with both equal does a balance difference count.
        /// </summary>
        public static HandshakeVerdict Compare(BuildIdentity local, BuildIdentity peer)
        {
            if (peer.protocol != local.protocol)
                return peer.protocol < local.protocol ? HandshakeVerdict.PeerOlder : HandshakeVerdict.PeerNewer;
            if (peer.sim != local.sim)
                return peer.sim < local.sim ? HandshakeVerdict.PeerOlder : HandshakeVerdict.PeerNewer;
            return peer.content == local.content ? HandshakeVerdict.Compatible : HandshakeVerdict.ContentMismatch;
        }

        private static byte[] SerializeIdentity(PacketType type, BuildIdentity self)
        {
            byte[] data = new byte[HANDSHAKE_BYTES];
            int offset = 0;
            data[offset++] = (byte)type;
            WriteUShort(data, ref offset, self.protocol);
            WriteUShort(data, ref offset, self.sim);
            WriteInt(data, ref offset, self.content);
            return data;
        }

        private static bool TryDeserializeIdentity(byte[] data, PacketType type, out BuildIdentity peer)
        {
            peer = default;
            if (data == null || data.Length != HANDSHAKE_BYTES || data[0] != (byte)type) return false;

            int offset = 1;
            peer.protocol = ReadUShort(data, ref offset);
            peer.sim = ReadUShort(data, ref offset);
            peer.content = ReadInt(data, ref offset);
            return true;
        }

        public static byte[] SerializeHeartbeat()
        {
            return new byte[] { (byte)PacketType.Heartbeat };
        }

        // ===== DRAFT ACK =====
        // A control packet, like the handshake ack and the heartbeat, so it sits
        // with them. DraftSerializer holds the packets it acknowledges.

        private const int DRAFT_ACK_BYTES = 1 + 1 + 4;
        private const byte DRAFT_ACK_READY = 1;
        private const byte DRAFT_ACK_LOADOUT = 2;

        /// <summary>
        /// DraftAck: [type:1][flags:1][placementsApplied:4] = 6 bytes.
        /// What the sender holds of the peer's draft packets: whether its ready
        /// and loadout have arrived, and how many placements (both players',
        /// in draft order) are on its board. Cumulative, so any later ack
        /// covers everything an earlier lost one did.
        /// </summary>
        public static byte[] SerializeDraftAck(bool readyReceived, bool loadoutReceived, int placementsApplied)
        {
            byte[] data = new byte[DRAFT_ACK_BYTES];
            int offset = 0;

            data[offset++] = (byte)PacketType.DraftAck;
            data[offset++] = (byte)((readyReceived ? DRAFT_ACK_READY : 0) |
                                    (loadoutReceived ? DRAFT_ACK_LOADOUT : 0));
            WriteInt(data, ref offset, placementsApplied);

            return data;
        }

        public static bool TryDeserializeDraftAck(byte[] data,
            out bool readyReceived, out bool loadoutReceived, out int placementsApplied)
        {
            readyReceived = false;
            loadoutReceived = false;
            placementsApplied = 0;

            if (data == null || data.Length != DRAFT_ACK_BYTES ||
                data[0] != (byte)PacketType.DraftAck) return false;

            int offset = 1; // skip PacketType byte
            byte flags = data[offset++];
            if ((flags & ~(DRAFT_ACK_READY | DRAFT_ACK_LOADOUT)) != 0) return false;
            readyReceived = (flags & DRAFT_ACK_READY) != 0;
            loadoutReceived = (flags & DRAFT_ACK_LOADOUT) != 0;
            placementsApplied = ReadInt(data, ref offset);
            if (placementsApplied < 0) return false;

            return true;
        }

        public static PacketType ReadPacketType(byte[] data)
        {
            if (data == null || data.Length == 0)
                return PacketType.Heartbeat; // safe fallback
            return (PacketType)data[0];
        }

        // --- Little-endian int read/write (platform-independent) ---

        private static void WriteInt(byte[] buffer, ref int offset, int value)
        {
            buffer[offset] = (byte)(value);
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            offset += 4;
        }

        private static void WriteUShort(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset] = (byte)(value);
            buffer[offset + 1] = (byte)(value >> 8);
            offset += 2;
        }

        private static ushort ReadUShort(byte[] buffer, ref int offset)
        {
            ushort value = (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
            offset += 2;
            return value;
        }

        private static int ReadInt(byte[] buffer, ref int offset)
        {
            int value = buffer[offset]
                      | (buffer[offset + 1] << 8)
                      | (buffer[offset + 2] << 16)
                      | (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }
    }
}
