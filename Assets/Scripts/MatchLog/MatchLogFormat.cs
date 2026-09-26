using System;
using System.Collections.Generic;
using System.Text;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    /// <summary>
    /// Chunk lengths allow unknown tags to be skipped. A changed known layout
    /// needs a new tag; FormatVersion changes only when the framing changes.
    /// All integers are explicitly little-endian, independent of the host.
    /// </summary>
    public static class MatchLogFormat
    {
        public const ushort FormatVersion = 1;
        internal const ushort HeaderTag = 1;
        internal const ushort BoardTag = 2;
        internal const ushort LoadoutsTag = 3;
        internal const ushort DraftTag = 4;
        internal const ushort TicksTag = 5;
        internal const ushort HashesTag = 6;
        internal const ushort ResultTag = 7;
        private const int BytesPerCommand = 24;

        public static byte[] Write(MatchLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (log.header == null || log.header.playerIds == null || log.header.playerIds.Length != 2)
                throw new ArgumentException("HEADER needs two player IDs.", nameof(log));
            if (log.loadouts == null || log.loadouts.Length != 2 ||
                log.loadouts[0] == null || log.loadouts[1] == null)
                throw new ArgumentException("LOADOUTS needs two players.", nameof(log));
            if (!ValidKind(log.header.kind)) throw new ArgumentException("Invalid match kind.", nameof(log));
            if (log.result != null && (!ValidReason(log.result.reason) ||
                log.result.winner < sbyte.MinValue || log.result.winner > sbyte.MaxValue))
                throw new ArgumentException("Invalid match result.", nameof(log));

            Writer file = new Writer();
            file.U8((byte)'N'); file.U8((byte)'W'); file.U8((byte)'M'); file.U8((byte)'L');
            file.U16(FormatVersion);
            Writer payload = new Writer();
            MatchLogHeader h = log.header;
            payload.U16(h.protocol); payload.U16(h.sim); payload.I32(h.content);
            payload.String(h.matchId); payload.String(h.playerIds[0]); payload.String(h.playerIds[1]);
            payload.U8(h.localPlayer); payload.I32(h.tier); payload.I32(h.seed);
            payload.I64(h.startUnixSeconds); payload.U8((byte)h.kind);
            file.Chunk(HeaderTag, payload);

            payload = new Writer();
            BoardConfigData b = log.board;
            payload.I32(b.gridCols); payload.I32(b.gridRows); payload.I32(b.defaultEdgeWeight);
            payload.I32(b.startingVillagersPerPlayer); payload.I32(b.startingFood);
            payload.I32(b.startingMaterials); payload.I32(b.startingMetal);
            payload.I32(b.ownedMultiplier); payload.I32(b.partiallyOwnedMultiplier);
            payload.I32(b.unownedMultiplier); payload.I32(b.enemyPartiallyOwnedMultiplier);
            payload.I32(b.enemyOwnedMultiplier);
            payload.I32(b.initialPlacements?.Length ?? 0);
            if (b.initialPlacements != null)
                foreach (BoardConfigData.InitialNodePlacement p in b.initialPlacements)
                {
                    payload.I32(p.gridX); payload.I32(p.gridZ); payload.I32((int)p.districtType);
                    payload.I32(p.ownerID); payload.I32(p.claimBar);
                }
            file.Chunk(BoardTag, payload);

            payload = new Writer();
            for (int i = 0; i < 2; i++)
            {
                payload.Ints(log.loadouts[i].suits);
                payload.Ints(log.loadouts[i].nodes);
            }
            file.Chunk(LoadoutsTag, payload);

            payload = new Writer();
            payload.I32(log.draft?.Length ?? 0);
            if (log.draft != null)
                foreach (DraftPlacement p in log.draft)
                {
                    payload.I32(p.playerID); payload.I32((int)p.districtType);
                    payload.I32(p.gridX); payload.I32(p.gridZ); payload.U8(p.wasTimeout ? (byte)1 : (byte)0);
                }
            file.Chunk(DraftTag, payload);

            payload = new Writer();
            payload.I32(log.ticks?.Count ?? 0);
            if (log.ticks != null)
                foreach (LoggedTick tick in log.ticks)
                {
                    int count = tick.commands?.Length ?? 0;
                    if (count > ushort.MaxValue)
                        throw new ArgumentException("Too many commands in one tick.", nameof(log));
                    payload.I32(tick.tick); payload.U16((ushort)count);
                    if (tick.commands != null)
                        foreach (GameCommand c in tick.commands)
                        {
                            payload.I32((int)c.type); payload.I32(c.playerID); payload.I32(c.villagerID);
                            payload.I32(c.targetNodeID); payload.I32(c.issuedOnTick); payload.I32(c.value);
                        }
                }
            file.Chunk(TicksTag, payload);

            payload = new Writer();
            payload.I32(log.hashes?.Count ?? 0);
            if (log.hashes != null)
                foreach (HashCheckpoint hash in log.hashes)
                {
                    payload.I32(hash.tick); payload.I32(hash.hash);
                }
            file.Chunk(HashesTag, payload);

            if (log.result != null)
            {
                payload = new Writer();
                payload.U8((byte)log.result.reason); payload.U8(unchecked((byte)log.result.winner));
                payload.I32(log.result.endTick); payload.I32(log.result.finalHash);
                payload.I32(log.result.firstDesyncTick);
                file.Chunk(ResultTag, payload);
            }
            return file.Bytes();
        }

        /// <summary>
        /// Reads untrusted bytes without exposing a partial log. Every count is
        /// bounded by its payload before allocation, including nested commands.
        /// EOF at a chunk boundary is valid once all required chunks are present;
        /// optional chunks cannot make such a prefix detectably truncated.
        /// </summary>
        public static bool TryRead(byte[] data, out MatchLog log, out string error)
        {
            log = null;
            error = null;
            try
            {
                if (data == null || data.Length < 6) throw new FormatException("File header is truncated.");
                Reader file = new Reader(data, 0, data.Length);
                if (file.U8() != 'N' || file.U8() != 'W' || file.U8() != 'M' || file.U8() != 'L')
                    throw new FormatException("Bad match log magic.");
                if (file.U16() != FormatVersion) throw new FormatException("Unsupported format version.");
                MatchLog parsed = new MatchLog
                {
                    draft = Array.Empty<DraftPlacement>(),
                    ticks = new List<LoggedTick>(),
                    hashes = new List<HashCheckpoint>()
                };
                int seen = 0;
                while (file.Remaining != 0)
                {
                    ushort tag = file.U16();
                    uint length = unchecked((uint)file.I32());
                    if (length > (uint)file.Remaining) throw new FormatException("Chunk payload is truncated.");
                    Reader payload = file.Slice((int)length);
                    if (tag < HeaderTag || tag > ResultTag) continue;
                    int bit = 1 << tag;
                    if ((seen & bit) != 0) throw new FormatException("Duplicate known chunk.");
                    seen |= bit;
                    ReadChunk(tag, payload, parsed);
                    if (payload.Remaining != 0) throw new FormatException("Trailing bytes in known chunk.");
                }
                const int required = (1 << HeaderTag) | (1 << BoardTag) | (1 << LoadoutsTag) | (1 << TicksTag);
                if ((seen & required) != required) throw new FormatException("Missing required chunk.");
                log = parsed;
                return true;
            }
            catch (FormatException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (Exception)
            {
                // This API is a failure boundary even if a bounded allocation
                // cannot be satisfied by the host. Never return a partial log.
                error = "Unable to read match log.";
                return false;
            }
        }

        private static void ReadChunk(ushort tag, Reader r, MatchLog log)
        {
            switch (tag)
            {
                case HeaderTag:
                    log.header = new MatchLogHeader
                    {
                        protocol = r.U16(), sim = r.U16(), content = r.I32(), matchId = r.String(),
                        playerIds = new[] { r.String(), r.String() }, localPlayer = r.U8(),
                        tier = r.I32(), seed = r.I32(), startUnixSeconds = r.I64(), kind = (MatchKind)r.U8()
                    };
                    if (!ValidKind(log.header.kind)) throw new FormatException("Invalid match kind.");
                    break;
                case BoardTag:
                    BoardConfigData b = new BoardConfigData
                    {
                        gridCols = r.I32(), gridRows = r.I32(), defaultEdgeWeight = r.I32(),
                        startingVillagersPerPlayer = r.I32(), startingFood = r.I32(),
                        startingMaterials = r.I32(), startingMetal = r.I32(),
                        ownedMultiplier = r.I32(), partiallyOwnedMultiplier = r.I32(),
                        unownedMultiplier = r.I32(), enemyPartiallyOwnedMultiplier = r.I32(),
                        enemyOwnedMultiplier = r.I32()
                    };
                    b.initialPlacements = new BoardConfigData.InitialNodePlacement[r.Count(20)];
                    for (int i = 0; i < b.initialPlacements.Length; i++)
                        b.initialPlacements[i] = new BoardConfigData.InitialNodePlacement
                        {
                            gridX = r.I32(), gridZ = r.I32(), districtType = (DistrictType)r.I32(),
                            ownerID = r.I32(), claimBar = r.I32()
                        };
                    log.board = b;
                    break;
                case LoadoutsTag:
                    log.loadouts = new PlayerLoadout[2];
                    for (int i = 0; i < 2; i++)
                        log.loadouts[i] = new PlayerLoadout { suits = r.Ints(), nodes = r.Ints() };
                    break;
                case DraftTag:
                    log.draft = new DraftPlacement[r.Count(17)];
                    for (int i = 0; i < log.draft.Length; i++)
                        log.draft[i] = new DraftPlacement
                        {
                            playerID = r.I32(), districtType = (DistrictType)r.I32(),
                            gridX = r.I32(), gridZ = r.I32(), wasTimeout = r.U8() != 0
                        };
                    break;
                case TicksTag:
                    int ticks = r.Count(6); // Even an empty tick needs its tick number and u16 count.
                    log.ticks = new List<LoggedTick>(ticks);
                    for (int i = 0; i < ticks; i++)
                    {
                        int tick = r.I32();
                        int count = r.U16();
                        r.CheckCount(count, BytesPerCommand);
                        GameCommand[] commands = new GameCommand[count];
                        for (int j = 0; j < count; j++)
                            commands[j] = new GameCommand
                            {
                                type = (CommandType)r.I32(), playerID = r.I32(), villagerID = r.I32(),
                                targetNodeID = r.I32(), issuedOnTick = r.I32(), value = r.I32()
                            };
                        log.ticks.Add(new LoggedTick { tick = tick, commands = commands });
                    }
                    break;
                case HashesTag:
                    int hashes = r.Count(8);
                    log.hashes = new List<HashCheckpoint>(hashes);
                    for (int i = 0; i < hashes; i++)
                        log.hashes.Add(new HashCheckpoint { tick = r.I32(), hash = r.I32() });
                    break;
                case ResultTag:
                    log.result = new MatchResult
                    {
                        reason = (MatchEndReason)r.U8(), winner = unchecked((sbyte)r.U8()),
                        endTick = r.I32(), finalHash = r.I32(), firstDesyncTick = r.I32()
                    };
                    if (!ValidReason(log.result.reason)) throw new FormatException("Invalid match end reason.");
                    break;
            }
        }

        private static bool ValidKind(MatchKind kind)
        {
            return kind == MatchKind.Local || kind == MatchKind.Bot || kind == MatchKind.Networked;
        }

        private static bool ValidReason(MatchEndReason reason)
        {
            return reason == MatchEndReason.Win || reason == MatchEndReason.Disconnect ||
                reason == MatchEndReason.Abandoned || reason == MatchEndReason.Surrender;
        }

        private sealed class Writer
        {
            private readonly List<byte> data = new List<byte>();
            public void U8(byte value) { data.Add(value); }
            public void U16(ushort value)
            {
                U8((byte)value); U8((byte)(value >> 8));
            }
            public void I32(int value)
            {
                U8((byte)value); U8((byte)(value >> 8));
                U8((byte)(value >> 16)); U8((byte)(value >> 24));
            }
            public void I64(long value)
            {
                I32(unchecked((int)value)); I32(unchecked((int)(value >> 32)));
            }
            public void String(string value)
            {
                value = value ?? string.Empty;
                int length = Encoding.UTF8.GetByteCount(value);
                if (length > ushort.MaxValue) throw new ArgumentException("UTF-8 string exceeds 65535 bytes.");
                U16((ushort)length);
                data.AddRange(Encoding.UTF8.GetBytes(value));
            }
            public void Ints(int[] values)
            {
                I32(values?.Length ?? 0);
                if (values != null)
                    foreach (int value in values) I32(value);
            }
            public void Chunk(ushort tag, Writer payload)
            {
                U16(tag); I32(payload.data.Count); data.AddRange(payload.data);
            }
            public byte[] Bytes() { return data.ToArray(); }
        }

        private sealed class Reader
        {
            private readonly byte[] data;
            private readonly int end;
            private int offset;
            public Reader(byte[] data, int offset, int length)
            {
                this.data = data;
                this.offset = offset;
                end = offset + length;
            }
            public int Remaining { get { return end - offset; } }
            private void Need(int bytes)
            {
                if (bytes > Remaining) throw new FormatException("Chunk fields are truncated.");
            }
            public byte U8() { Need(1); return data[offset++]; }
            public ushort U16()
            {
                Need(2);
                int value = data[offset] | (data[offset + 1] << 8);
                offset += 2;
                return (ushort)value;
            }
            public int I32()
            {
                Need(4);
                int value = data[offset] | (data[offset + 1] << 8) |
                    (data[offset + 2] << 16) | (data[offset + 3] << 24);
                offset += 4;
                return value;
            }
            public long I64()
            {
                uint low = unchecked((uint)I32());
                return ((long)I32() << 32) | low;
            }
            public string String()
            {
                int length = U16();
                Need(length);
                string value = Encoding.UTF8.GetString(data, offset, length);
                offset += length;
                return value;
            }
            public int Count(int elementSize)
            {
                int count = I32();
                CheckCount(count, elementSize);
                return count;
            }
            public void CheckCount(int count, int elementSize)
            {
                // Divide, never multiply: an attacker-controlled count must not
                // overflow into a small allocation or a successful size check.
                if (count < 0 || count > Remaining / elementSize)
                    throw new FormatException("Invalid count in chunk.");
            }
            public int[] Ints()
            {
                int[] values = new int[Count(4)];
                for (int i = 0; i < values.Length; i++) values[i] = I32();
                return values;
            }
            public Reader Slice(int length)
            {
                Need(length);
                Reader slice = new Reader(data, offset, length);
                offset += length;
                return slice;
            }
        }
    }
}
