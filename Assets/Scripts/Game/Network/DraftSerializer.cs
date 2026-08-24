namespace NodeWar.Network
{
    /// <summary>
    /// Serializes and deserializes draft-phase packets.
    /// Follows the same byte-level pattern as InputSerializer.
    /// </summary>
    public static class DraftSerializer
    {
        /// <summary>
        /// DraftReady: [type:1][playerID:4] = 5 bytes
        /// </summary>
        public static byte[] SerializeDraftReady(int playerID)
        {
            byte[] data = new byte[5];
            data[0] = (byte)PacketType.DraftReady;
            WriteInt(data, 1, playerID);
            return data;
        }

        public static int DeserializeDraftReady(byte[] data)
        {
            return ReadInt(data, 1);
        }

        /// <summary>
        /// DraftPlacement: [type:1][playerID:4][districtType:4][gridX:4][gridZ:4][wasTimeout:1] = 18 bytes
        /// </summary>
        public static byte[] SerializeDraftPlacement(
            int playerID, int districtType, int gridX, int gridZ, bool wasTimeout)
        {
            byte[] data = new byte[18];
            int offset = 0;
            data[offset++] = (byte)PacketType.DraftPlacement;
            WriteInt(data, ref offset, playerID);
            WriteInt(data, ref offset, districtType);
            WriteInt(data, ref offset, gridX);
            WriteInt(data, ref offset, gridZ);
            data[offset] = (byte)(wasTimeout ? 1 : 0);
            return data;
        }

        public static void DeserializeDraftPlacement(byte[] data,
            out int playerID, out int districtType, out int gridX, out int gridZ, out bool wasTimeout)
        {
            int offset = 1; // skip type byte
            playerID = ReadInt(data, ref offset);
            districtType = ReadInt(data, ref offset);
            gridX = ReadInt(data, ref offset);
            gridZ = ReadInt(data, ref offset);
            wasTimeout = data[offset] != 0;
        }

        // Little-endian helpers (same as InputSerializer)
        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value);
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt(byte[] buffer, ref int offset, int value)
        {
            buffer[offset] = (byte)(value);
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            offset += 4;
        }

        private static int ReadInt(byte[] buffer, int offset)
        {
            return buffer[offset]
                 | (buffer[offset + 1] << 8)
                 | (buffer[offset + 2] << 16)
                 | (buffer[offset + 3] << 24);
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