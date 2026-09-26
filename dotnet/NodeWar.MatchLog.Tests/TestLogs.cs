using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    internal static class TestLogs
    {
        public static MatchLog Full()
        {
            return new MatchLog
            {
                header = new MatchLogHeader
                {
                    protocol = 0x1234, sim = 0x5678, content = -123456789,
                    matchId = "match-42", playerIds = new[] { "玩家-é", "player-two" },
                    localPlayer = 1, tier = 7, seed = -987654321,
                    startUnixSeconds = 0x0102030405060708L, kind = MatchKind.Networked
                },
                board = new BoardConfigData
                {
                    gridCols = 5, gridRows = 9, defaultEdgeWeight = 3,
                    startingVillagersPerPlayer = 4, startingFood = 17, startingMaterials = 29,
                    startingMetal = 31, ownedMultiplier = 51, partiallyOwnedMultiplier = 76,
                    unownedMultiplier = 101, enemyPartiallyOwnedMultiplier = 151, enemyOwnedMultiplier = 201,
                    initialPlacements = new[]
                    {
                        new BoardConfigData.InitialNodePlacement
                        { gridX = 1, gridZ = 8, districtType = DistrictType.Core, ownerID = 0, claimBar = 10000 },
                        new BoardConfigData.InitialNodePlacement
                        { gridX = 3, gridZ = 0, districtType = DistrictType.Forge, ownerID = 1, claimBar = -8765 }
                    }
                },
                loadouts = new[]
                {
                    new PlayerLoadout { suits = new[] { 2, 4, 6 }, nodes = new[] { 3, 5 } },
                    new PlayerLoadout { suits = new[] { 7, 8 }, nodes = new[] { 9, 10, 11 } }
                },
                draft = new[]
                {
                    new DraftPlacement { playerID = 0, districtType = DistrictType.Forge, gridX = 2, gridZ = 7 },
                    new DraftPlacement { playerID = 1, districtType = DistrictType.Core, gridX = 4, gridZ = 1, wasTimeout = true }
                },
                ticks = new List<LoggedTick>
                {
                    new LoggedTick { tick = 4, commands = new[]
                    {
                        Command(CommandType.Move, 1, 11, 4, 3, -2),
                        Command(CommandType.SetAllocation, 0, -1, 8, 2, 37),
                        Command(CommandType.Move, 1, 12, 5, 4, 0)
                    } },
                    new LoggedTick { tick = 53, commands = new[]
                    {
                        Command(CommandType.Move, 0, 2, 3, 52, 1),
                        Command(CommandType.SetAllocation, 1, -1, 7, 51, 19)
                    } }
                },
                hashes = new List<HashCheckpoint>
                {
                    new HashCheckpoint { tick = 50, hash = int.MinValue },
                    new HashCheckpoint { tick = 100, hash = 0x12345678 }
                },
                result = new MatchResult
                { reason = MatchEndReason.Disconnect, winner = -1, endTick = 120, finalHash = -543210, firstDesyncTick = 50 }
            };
        }

        public static GameCommand Command(CommandType type, int player, int villager, int target, int issued, int value = 0)
        {
            return new GameCommand
            { type = type, playerID = player, villagerID = villager, targetNodeID = target, issuedOnTick = issued, value = value };
        }

        // Compare every public data field, including nested arrays and lists.
        // Re-encoding alone could hide a symmetric reader/writer omission.
        public static void Equal(object expected, object actual, string path = "log")
        {
            if (expected == null) { Assert.IsNull(actual, path); return; }
            Assert.IsNotNull(actual, path);
            Type type = expected.GetType();
            Assert.AreEqual(type, actual.GetType(), path);
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            { Assert.AreEqual(expected, actual, path); return; }
            if (expected is IList list)
            {
                IList other = (IList)actual;
                Assert.AreEqual(list.Count, other.Count, path + ".Count");
                for (int i = 0; i < list.Count; i++) Equal(list[i], other[i], path + "[" + i + "]");
                return;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Equal(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
        }

        public static MatchLog Read(byte[] data)
        {
            Assert.IsTrue(MatchLogFormat.TryRead(data, out MatchLog log, out string error), error);
            Assert.IsNull(error);
            return log;
        }

        public static void Refused(byte[] data, string context = null)
        {
            MatchLog log = null;
            string error = null;
            bool success = true;
            Assert.DoesNotThrow(() => success = MatchLogFormat.TryRead(data, out log, out error), context);
            Assert.IsFalse(success, context);
            Assert.IsNull(log, context);
            Assert.IsNotEmpty(error, context);
        }

        public static int IntAt(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
        }

        public static void PutInt(byte[] data, int offset, int value)
        {
            for (int i = 0; i < 4; i++) data[offset + i] = (byte)(value >> (8 * i));
        }

        public static int Find(byte[] data, int tag)
        {
            for (int offset = 6; offset < data.Length; offset += 6 + IntAt(data, offset + 2))
                if ((data[offset] | data[offset + 1] << 8) == tag) return offset;
            throw new InvalidOperationException("Missing test chunk " + tag);
        }

        public static byte[] Segment(byte[] data, int offset, int length)
        {
            byte[] copy = new byte[length];
            Array.Copy(data, offset, copy, 0, length);
            return copy;
        }

        public static byte[] Insert(byte[] data, int offset, byte[] extra)
        {
            var bytes = new List<byte>(data);
            bytes.InsertRange(offset, extra);
            return bytes.ToArray();
        }

        public static byte[] Remove(byte[] data, int tag)
        {
            int start = Find(data, tag);
            var bytes = new List<byte>(data);
            bytes.RemoveRange(start, 6 + IntAt(data, start + 2));
            return bytes.ToArray();
        }
    }
}
