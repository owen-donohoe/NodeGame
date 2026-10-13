using System;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class NodeCommandWireTests
    {
        [TestCase(0)] [TestCase(1)]
        public void Collect_RoundTripsStartCancelAllFields(int value)
        {
            Assert.AreEqual("Collect", Enum.GetName(typeof(CommandType), 9));
            var command = new GameCommand { type = (CommandType)9, playerID = 1, villagerID = -1, targetNodeID = 18, issuedOnTick = 123, value = value };
            byte[] bytes = InputSerializer.Serialize(new TickInput { forTick = 123, commands = new[] { command } });
            Assert.AreEqual(39, bytes.Length); Assert.IsTrue(InputSerializer.TryDeserialize(bytes, out var read)); Assert.AreEqual(command, read.commands[0]);
        }

        [Test]
        public void CurrentPacket_Market13IsRefused_Storehouse18Accepted()
        {
            foreach (string id in new[] { "node_market", "node_storehouse" }) {
                var loadout = NodeWar.Lobby.LoadoutData.CreateEmpty(); loadout.districtIDs[0] = id;
                byte[] packet = DraftSerializer.SerializeDraftLoadout(0, loadout);
                if (id == "node_market") Assert.Throws<FormatException>(() => DraftSerializer.DeserializeDraftLoadout(packet, out _, out _));
                else { DraftSerializer.DeserializeDraftLoadout(packet, out _, out var read); Assert.AreEqual(id, read.districtIDs[0]); }
            }
        }
        [TestCase(0)] [TestCase(1)]
        public void UpgradeFortress_RoundTripsBothCurrencies(int currency)
        {
            Assert.AreEqual("UpgradeFortress", Enum.GetName(typeof(CommandType), 7));
            var command = new GameCommand { type = (CommandType)7, playerID = 1, villagerID = -1, targetNodeID = 17, issuedOnTick = 123, value = currency };
            byte[] bytes = InputSerializer.Serialize(new TickInput { forTick = 123, commands = new[] { command } });
            Assert.AreEqual(39, bytes.Length); Assert.IsTrue(InputSerializer.TryDeserialize(bytes, out var read)); Assert.AreEqual(command, read.commands[0]);
        }
        [TestCase(8, 0, 0)] [TestCase(8, 1, 0)] [TestCase(5, 0, 0)] [TestCase(5, 1, 0)]
        [TestCase(6, 0, 0)] [TestCase(6, 0, 1)] [TestCase(6, 1, 0)] [TestCase(6, 1, 1)]
        public void RecruitAndSetAuto_RoundTripAllFields(int type, int player, int value)
        {
            Assert.AreEqual(type == 8 ? "ForgeMinion" : type == 5 ? "Recruit" : "SetAutoRecruit", Enum.GetName(typeof(CommandType), type));
            var command = new GameCommand { type = (CommandType)type, playerID = player, villagerID = -1,
                targetNodeID = 17, issuedOnTick = 123, value = value };
            byte[] bytes = InputSerializer.Serialize(new TickInput { forTick = 123, commands = new[] { command } });
            Assert.AreEqual(39, bytes.Length);
            Assert.IsTrue(InputSerializer.TryDeserialize(bytes, out var read));
            Assert.AreEqual(command, read.commands[0]);
        }

        [TestCase(-1)] [TestCase(10)] [TestCase(int.MaxValue)]
        public void UnknownCommandType_IsRefused(int type)
        {
            byte[] bytes = InputSerializer.Serialize(new TickInput { commands = new[] { new GameCommand() } });
            Array.Copy(BitConverter.GetBytes(type), 0, bytes, 15, 4);
            Assert.IsFalse(InputSerializer.TryDeserialize(bytes, out _));
            Assert.Throws<ArgumentException>(() => InputSerializer.Serialize(new TickInput
                { commands = new[] { new GameCommand { type = (CommandType)type } } }));
        }
    }
}
