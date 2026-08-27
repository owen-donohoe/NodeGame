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

        /// <summary>
        /// DraftLoadout: [type:1][playerID:4][suit0Len:1][suit0:N][suit1Len:1][suit1:N][suit2Len:1][suit2:N]
        ///              [node0Len:1][node0:N][node1Len:1][node1:N]
        /// Variable length. String IDs encoded as length-prefixed UTF8.
        /// </summary>
        public static byte[] SerializeDraftLoadout(int playerID, NodeWar.Lobby.LoadoutData loadout)
        {
            // Calculate total size
            byte[] suit0Bytes = System.Text.Encoding.UTF8.GetBytes(loadout.suitID0 ?? "");
            byte[] suit1Bytes = System.Text.Encoding.UTF8.GetBytes(loadout.suitID1 ?? "");
            byte[] suit2Bytes = System.Text.Encoding.UTF8.GetBytes(loadout.suitID2 ?? "");
            byte[] node0Bytes = System.Text.Encoding.UTF8.GetBytes(loadout.nodeID0 ?? "");
            byte[] node1Bytes = System.Text.Encoding.UTF8.GetBytes(loadout.nodeID1 ?? "");

            int size = 1 + 4 + 5 + suit0Bytes.Length + suit1Bytes.Length + suit2Bytes.Length
                     + node0Bytes.Length + node1Bytes.Length;

            byte[] data = new byte[size];
            int offset = 0;

            data[offset++] = (byte)PacketType.DraftLoadout;
            WriteInt(data, ref offset, playerID);

            data[offset++] = (byte)suit0Bytes.Length;
            System.Array.Copy(suit0Bytes, 0, data, offset, suit0Bytes.Length);
            offset += suit0Bytes.Length;

            data[offset++] = (byte)suit1Bytes.Length;
            System.Array.Copy(suit1Bytes, 0, data, offset, suit1Bytes.Length);
            offset += suit1Bytes.Length;

            data[offset++] = (byte)suit2Bytes.Length;
            System.Array.Copy(suit2Bytes, 0, data, offset, suit2Bytes.Length);
            offset += suit2Bytes.Length;

            data[offset++] = (byte)node0Bytes.Length;
            System.Array.Copy(node0Bytes, 0, data, offset, node0Bytes.Length);
            offset += node0Bytes.Length;

            data[offset++] = (byte)node1Bytes.Length;
            System.Array.Copy(node1Bytes, 0, data, offset, node1Bytes.Length);
            offset += node1Bytes.Length;

            return data;
        }

        public static void DeserializeDraftLoadout(byte[] data,
            out int playerID, out NodeWar.Lobby.LoadoutData loadout)
        {
            int offset = 1; // skip type byte
            playerID = ReadInt(data, ref offset);

            loadout = new NodeWar.Lobby.LoadoutData();

            int len;
            len = data[offset++];
            loadout.suitID0 = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;

            len = data[offset++];
            loadout.suitID1 = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;

            len = data[offset++];
            loadout.suitID2 = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;

            len = data[offset++];
            loadout.nodeID0 = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;

            len = data[offset++];
            loadout.nodeID1 = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;
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