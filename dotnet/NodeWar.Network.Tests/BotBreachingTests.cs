using System;
using NUnit.Framework;
using NodeWar.Input;
using NodeWar.Simulation;

namespace NodeWar.Network.Tests
{
    public class BotBreachingTests
    {
        [TestCase(2, 1)] [TestCase(3, 2)]
        public void BotReservesEscalatingPricesForSameTickBatch(int food, int expectedRespawns)
        {
            var balance = GameBalanceData.Default();
            var board = BoardConfigData.Default();
            MatchFactory.Configure(balance, board);
            var state = MatchFactory.Build(balance, board, new DraftPlacement[0], new PlayerSetup[0]);
            state.players[0].food = food;
            for (int i = 0; i < state.villagers.Length; i++)
                state.villagers[i].state = VillagerState.Dead;
            var buffer = new InputBuffer();
            new BotPlayer(state, buffer, 0, board.defaultLinkWeight).Evaluate();
            var commands = buffer.DrainCommands();
            Assert.AreEqual(expectedRespawns, Array.FindAll(commands, c => c.type == CommandType.Respawn).Length);
            foreach (var command in commands) CommandProcessor.ProcessCommand(state, command);
            Assert.AreEqual(expectedRespawns, state.players[0].paidRespawns);
        }
        [TestCase(SuitType.None)] [TestCase(SuitType.Warrior)]
        public void CoreEmergencyPreservesBreacherAndSendsAvailableDefender(SuitType suit)
        {
            var b = GameBalanceData.Default();
            MatchFactory.Configure(b, BoardConfigData.Default());
            var state = new SimulationState
            {
                players = new[] { new PlayerData { playerID = 0, coreNodeID = 0 }, new PlayerData { playerID = 1, coreNodeID = 2 } },
                nodes = new[]
                {
                    new NodeData { nodeID = 0, ownerID = 0, districtType = DistrictType.Core, links = new[] { new Link { toNodeID = 1, travelWeight = 1 } } },
                    new NodeData { nodeID = 1, ownerID = -1, links = new[] { new Link { toNodeID = 0, travelWeight = 1 }, new Link { toNodeID = 2, travelWeight = 1 } } },
                    new NodeData { nodeID = 2, ownerID = 1, districtType = DistrictType.Core, links = new[] { new Link { toNodeID = 1, travelWeight = 1 } } }
                },
                villagers = new[]
                {
                    new VillagerData { villagerID = 0, ownerID = 0, currentNodeID = 2, targetNodeID = -1, state = VillagerState.Breaching, suit = suit, moveSpeedTicks = 4, movePath = Array.Empty<int>() },
                    new VillagerData { villagerID = 1, ownerID = 1, currentNodeID = 0, targetNodeID = -1, state = VillagerState.Breaching, movePath = Array.Empty<int>() },
                    new VillagerData { villagerID = 2, ownerID = 0, currentNodeID = 1, targetNodeID = -1, state = VillagerState.Idle, moveSpeedTicks = 4, movePath = Array.Empty<int>() }
                }
            };
            var buffer = new InputBuffer();
            new BotPlayer(state, buffer, 0, 1).Evaluate();
            var commands = buffer.DrainCommands();
            Assert.IsTrue(Array.Exists(commands, c => c.type == CommandType.Move && c.villagerID == 2 && c.targetNodeID == 0));
            Assert.IsFalse(Array.Exists(commands, c => c.villagerID == 0), "Breacher must remain an attacker on target.");
            Assert.AreEqual(VillagerState.Breaching, state.villagers[0].state);
        }
    }
}
