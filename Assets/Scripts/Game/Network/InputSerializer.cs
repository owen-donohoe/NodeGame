using NodeWar.Simulation;

namespace NodeWar.Network
{
    public enum PacketType : byte
    {
        Handshake = 0,
        HandshakeAck = 1,
        TickInput = 2,
        Heartbeat = 3,
        DraftReady = 4,
        DraftPlacement = 5
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
        private const int BYTES_PER_COMMAND = 24;

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

        public static TickInput Deserialize(byte[] data)
        {
            int offset = 1; // skip PacketType byte
            TickInput input = new TickInput();

            input.forTick = ReadInt(data, ref offset);
            input.stateHash = ReadInt(data, ref offset);
            int commandCount = ReadInt(data, ref offset);

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

            return input;
        }

        public static byte[] SerializeHandshake()
        {
            return new byte[] { (byte)PacketType.Handshake };
        }

        public static byte[] SerializeHandshakeAck()
        {
            return new byte[] { (byte)PacketType.HandshakeAck };
        }

        public static byte[] SerializeHeartbeat()
        {
            return new byte[] { (byte)PacketType.Heartbeat };
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