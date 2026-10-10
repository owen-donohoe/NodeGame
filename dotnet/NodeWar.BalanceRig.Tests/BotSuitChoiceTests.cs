using System;
using NodeWar.Input;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.BalanceRig
{
    public class BotSuitChoiceTests
    {
        private static SimulationState State(params SuitType[] suits) => new SimulationState
        {
            nodes = new[] { new NodeData { ownerID = 0, districtType = DistrictType.Barracks } },
            players = new[] { new PlayerData { food = 10, materials = 10,
                draftedSuits = Array.ConvertAll(suits, s => (int)s), suitEras = new int[16] } }
        };

        private static GameBalanceData Balance() {
            var b = GameBalanceData.Default();
            b.suitStats = new[] {
                new SuitStats { suitType = SuitType.Warrior, foodCost = 2, materialCost = 1 },
                new SuitStats { suitType = SuitType.Guardian, foodCost = 3, materialCost = 2 },
                new SuitStats { suitType = SuitType.Scout, foodCost = 1 } };
            return b;
        }

        [Test]
        public void WarriorAbsent_ChoosesDraftedAffordableAlternative()
        {
            Assert.That(BotSuitChoice.Choose(State(SuitType.Scout, SuitType.Guardian), Balance(), 0, 0),
                Is.EqualTo(SuitType.Guardian));
        }

        [Test]
        public void LegacyWarriorPreference_Unchanged()
        {
            Assert.That(BotSuitChoice.Choose(State(SuitType.Guardian, SuitType.Warrior), Balance(), 0, 0),
                Is.EqualTo(SuitType.Warrior));
        }

        [TestCase("undrafted")]
        [TestCase("unaffordable")]
        [TestCase("wrongNode")]
        [TestCase("enemyNode")]
        [TestCase("missingStats")]
        public void NoEligibleSuit_NoEquip(string reason)
        {
            var s = State(SuitType.Guardian);
            var b = Balance();
            if (reason == "undrafted") s.players[0].draftedSuits = Array.Empty<int>();
            if (reason == "unaffordable") s.players[0].materials = 1;
            if (reason == "wrongNode") s.nodes[0].districtType = DistrictType.Core;
            if (reason == "enemyNode") s.nodes[0].ownerID = 1;
            if (reason == "missingStats") b.suitStats = Array.Empty<SuitStats>();
            Assert.That(BotSuitChoice.Choose(s, b, 0, 0), Is.EqualTo(SuitType.None));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BotPlayer_UsesInjectedBalanceAndStampsIssuingTick(bool affordable)
        {
            var s = State(SuitType.Guardian);
            s.players = new[] { s.players[0], new PlayerData { coreNodeID = 0 } };
            s.nodes[0].links = Array.Empty<Link>();
            s.villagers = new[] { new VillagerData { ownerID = 0, currentNodeID = 0,
                state = VillagerState.Idle, suit = SuitType.None } };
            s.tickCount = 23;
            var b = Balance();
            b.suitStats = new[] { new SuitStats { suitType = SuitType.Guardian, foodCost = affordable ? 10 : 11 } };
            var buffer = new InputBuffer();
            new BotPlayer(s, buffer, 0, 4, b).Evaluate();
            var commands = buffer.DrainCommands();
            if (!affordable) Assert.That(commands, Is.Empty);
            else {
                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(commands[0].type, Is.EqualTo(CommandType.Equip));
                Assert.That(commands[0].value, Is.EqualTo((int)SuitType.Guardian));
                Assert.That(commands[0].issuedOnTick, Is.EqualTo(23));
            }
        }

        [Test]
        public void CustomBalanceAndCurrentEra_DetermineAffordability()
        {
            var s = State(SuitType.Warrior, SuitType.Guardian);
            s.players[0].suitEras[(int)SuitType.Warrior] = 1;
            var b = Balance();
            b.suitStats = new[] {
                new SuitStats { suitType = SuitType.Warrior, foodCost = 1 },
                new SuitStats { suitType = SuitType.Warrior, era = 1, foodCost = 11 },
                new SuitStats { suitType = SuitType.Guardian, foodCost = 10 } };
            Assert.That(BotSuitChoice.Choose(s, b, 0, 0), Is.EqualTo(SuitType.Guardian));
        }
    }
}
