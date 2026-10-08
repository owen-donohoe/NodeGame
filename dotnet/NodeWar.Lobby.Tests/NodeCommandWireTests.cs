using System;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class NodeCommandWireTests
    {
        [TestCase(0)] [TestCase(1)]
        public void UpgradeFortress_RoundTripsBothCurrencies(int currency)
        {
            Assert.AreEqual("UpgradeFortress", Enum.GetName(typeof(CommandType), 7));
            var command = new GameCommand { type = (CommandType)7, playerID = 1, villagerID = -1, targetNodeID = 17, issuedOnTick = 123, value = currency };
            byte[] bytes = InputSerializer.Serialize(new TickInput { forTick = 123, commands = new[] { command } });
            Assert.AreEqual(39, bytes.Length); Assert.IsTrue(InputSerializer.TryDeserialize(bytes, out var read)); Assert.AreEqual(command, read.commands[0]);
        }
        [TestCase(5, 0, 0)] [TestCase(5, 1, 0)]
        [TestCase(6, 0, 0)] [TestCase(6, 0, 1)] [TestCase(6, 1, 0)] [TestCase(6, 1, 1)]
        public void RecruitAndSetAuto_RoundTripAllFields(int type, int player, int value)
        {
            Assert.AreEqual(type == 5 ? "Recruit" : "SetAutoRecruit", Enum.GetName(typeof(CommandType), type));
            var command = new GameCommand { type = (CommandType)type, playerID = player, villagerID = -1,
                targetNodeID = 17, issuedOnTick = 123, value = value };
            byte[] bytes = InputSerializer.Serialize(new TickInput { forTick = 123, commands = new[] { command } });
            Assert.AreEqual(39, bytes.Length);
            Assert.IsTrue(InputSerializer.TryDeserialize(bytes, out var read));
            Assert.AreEqual(command, read.commands[0]);
        }

        [TestCase(-1)] [TestCase(8)] [TestCase(int.MaxValue)]
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
