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
        /// DraftLoadout: [type:1][playerID:4]
        ///               [suitCount:1][ (len:1)(utf8:len) x suitCount ]
        ///               [nodeCount:1][ (len:1)(utf8:len) x nodeCount ]
        ///
        /// Variable length. String IDs are length-prefixed UTF8, and each array
        /// is count-prefixed, so changing LoadoutData.SuitSlots or NodeSlots
        /// does not change this format. Both peers still have to be on the same
        /// build — a count mismatch is a build mismatch, which desyncs anyway.
        ///
        /// One byte per length caps an ID at 255 bytes; IDs are short ASCII
        /// like "suit_warrior", and an over-long one is clamped rather than
        /// allowed to corrupt the offset of everything after it.
        /// </summary>
        public static byte[] SerializeDraftLoadout(int playerID, NodeWar.Lobby.LoadoutData loadout)
        {
            // Normalize first, so a default-constructed or foreign-shaped
            // loadout still produces a well-formed packet.
            loadout = NodeWar.Lobby.LoadoutData.Normalized(loadout);

            byte[][] suitBytes = EncodeAll(loadout.suitIDs);
            byte[][] nodeBytes = EncodeAll(loadout.nodeIDs);

            int size = 1 + 4                        // type, playerID
                     + 1 + MeasureAll(suitBytes)    // suit count + entries
                     + 1 + MeasureAll(nodeBytes);   // node count + entries

            byte[] data = new byte[size];
            int offset = 0;

            data[offset++] = (byte)PacketType.DraftLoadout;
            WriteInt(data, ref offset, playerID);
            WriteStringArray(data, ref offset, suitBytes);
            WriteStringArray(data, ref offset, nodeBytes);

            return data;
        }

        public static void DeserializeDraftLoadout(byte[] data,
            out int playerID, out NodeWar.Lobby.LoadoutData loadout)
        {
            int offset = 1; // skip type byte
            playerID = ReadInt(data, ref offset);

            loadout = new NodeWar.Lobby.LoadoutData
            {
                suitIDs = ReadStringArray(data, ref offset),
                nodeIDs = ReadStringArray(data, ref offset)
            };

            // Reconcile with this build's slot counts before anyone reads it.
            loadout = NodeWar.Lobby.LoadoutData.Normalized(loadout);
        }

        // ===== LENGTH-PREFIXED STRING ARRAYS =====

        private const int MaxStringBytes = 255;

        private static byte[][] EncodeAll(string[] values)
        {
            byte[][] encoded = new byte[values.Length][];

            for (int i = 0; i < values.Length; i++)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(values[i] ?? "");

                if (bytes.Length > MaxStringBytes)
                {
                    byte[] clamped = new byte[MaxStringBytes];
                    System.Array.Copy(bytes, clamped, MaxStringBytes);
                    bytes = clamped;
                }

                encoded[i] = bytes;
            }

            return encoded;
        }

        private static int MeasureAll(byte[][] encoded)
        {
            int total = 0;
            for (int i = 0; i < encoded.Length; i++)
                total += 1 + encoded[i].Length; // length byte + payload
            return total;
        }

        private static void WriteStringArray(byte[] buffer, ref int offset, byte[][] encoded)
        {
            buffer[offset++] = (byte)encoded.Length;

            for (int i = 0; i < encoded.Length; i++)
            {
                buffer[offset++] = (byte)encoded[i].Length;
                System.Array.Copy(encoded[i], 0, buffer, offset, encoded[i].Length);
                offset += encoded[i].Length;
            }
        }

        private static string[] ReadStringArray(byte[] buffer, ref int offset)
        {
            int count = buffer[offset++];
            string[] values = new string[count];

            for (int i = 0; i < count; i++)
            {
                int len = buffer[offset++];
                values[i] = System.Text.Encoding.UTF8.GetString(buffer, offset, len);
                offset += len;
            }

            return values;
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