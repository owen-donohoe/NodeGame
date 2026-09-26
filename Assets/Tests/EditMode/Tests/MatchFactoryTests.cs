using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// The starting board every match, replay and headless run begins from.
    /// These pin what GameManager built by hand before MatchFactory existed;
    /// a change here changes every match, so it is a SimulationVersion bump.
    /// </summary>
    public class MatchFactoryTests
    {
        private static GameBalanceData Balance()
        {
            return GameBalanceData.Default();
        }

        private static PlayerSetup[] Setups()
        {
            return new[]
            {
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior, (int)SuitType.Scout }, nodes = new[] { (int)DistrictType.Rampart } },
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior }, nodes = new int[0] }
            };
        }

        [Test]
        public void DefaultBoard_CoresBelongToTheirPlayersByPosition()
        {
            GameBalanceData balance = Balance();
            SimulationState state = MatchFactory.Build(balance, BoardConfigData.Default(), null, Setups());

            Assert.AreEqual(28, state.nodes.Length);
            Assert.AreEqual(25, state.players[0].coreNodeID, "P0 owns the highest-Z core (1,6).");
            Assert.AreEqual(2, state.players[1].coreNodeID, "P1 owns the lowest-Z core (2,0).");
            Assert.AreEqual(0, state.nodes[25].ownerID);
            Assert.AreEqual(balance.claimThreshold, state.nodes[25].claimBar);
            Assert.AreEqual(1, state.nodes[2].ownerID);
            Assert.AreEqual(-balance.claimThreshold, state.nodes[2].claimBar);
            Assert.AreEqual(DistrictType.Core, state.nodes[25].districtType);
            Assert.AreEqual(DistrictType.None, state.nodes[0].districtType);
            Assert.AreEqual(-1, state.nodes[0].ownerID);
        }

        [Test]
        public void MisconfiguredCoreOwners_AreCorrected()
        {
            BoardConfigData board = BoardConfigData.Default();
            board.initialPlacements[0].ownerID = 1; // (1,6) claims to be P1's
            board.initialPlacements[1].ownerID = 0;

            SimulationState state = MatchFactory.Build(Balance(), board, null, Setups());

            Assert.AreEqual(0, state.nodes[25].ownerID);
            Assert.AreEqual(1, state.nodes[2].ownerID);
        }

        [Test]
        public void Edges_AreLeftRightDownUp()
        {
            SimulationState state = MatchFactory.Build(Balance(), BoardConfigData.Default(), null, Setups());

            Edge[] middle = state.nodes[5].edges; // (1,1)
            Assert.AreEqual(new[] { 4, 6, 1, 9 }, new[] { middle[0].toNode, middle[1].toNode, middle[2].toNode, middle[3].toNode });
            Edge[] corner = state.nodes[0].edges;
            Assert.AreEqual(2, corner.Length);
            Assert.AreEqual(1, corner[0].toNode);
            Assert.AreEqual(4, corner[1].toNode);
            Assert.AreEqual(BoardConfigData.DefaultEdgeWeight, corner[0].travelWeight);
        }

        [Test]
        public void DraftPlacements_AreUnownedAndVillagesCarryTheirBonus()
        {
            GameBalanceData balance = Balance();
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Village, gridX = 0, gridZ = 3 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 3, gridZ = 3 }
            };

            SimulationState state = MatchFactory.Build(balance, BoardConfigData.Default(), draft, Setups());

            Assert.AreEqual(DistrictType.Village, state.nodes[12].districtType);
            Assert.AreEqual(DistrictType.Village, state.nodes[12].baseDistrictType);
            Assert.AreEqual(-1, state.nodes[12].ownerID);
            Assert.AreEqual(balance.bonusVillagersOnVillageClaim, state.nodes[12].bonusVillagersOnClaim);
            Assert.AreEqual(DistrictType.Farm, state.nodes[15].districtType);
            Assert.AreEqual(0, state.nodes[15].bonusVillagersOnClaim);
        }

        [Test]
        public void Players_GetStartingResourcesAndTheirDraftedTypes()
        {
            BoardConfigData board = BoardConfigData.Default();
            board.startingFood = 3;
            board.startingMaterials = 4;
            board.startingMetal = 5;

            SimulationState state = MatchFactory.Build(Balance(), board, null, Setups());

            Assert.AreEqual(3, state.players[1].food);
            Assert.AreEqual(4, state.players[1].materials);
            Assert.AreEqual(5, state.players[1].metal);
            Assert.AreEqual(new[] { (int)SuitType.Warrior, (int)SuitType.Scout }, state.players[0].draftedSuits);
            Assert.AreEqual(new[] { (int)DistrictType.Rampart }, state.players[0].draftedNodes);
            Assert.AreEqual(new int[0], state.players[1].draftedNodes);
        }

        [Test]
        public void Villagers_StartIdleOnTheirCore_P0First()
        {
            GameBalanceData balance = Balance();
            SimulationState state = MatchFactory.Build(balance, BoardConfigData.Default(), null, Setups());

            Assert.AreEqual(6, state.villagers.Length);
            for (int i = 0; i < 6; i++)
            {
                int owner = i < 3 ? 0 : 1;
                Assert.AreEqual(i, state.villagers[i].villagerID);
                Assert.AreEqual(owner, state.villagers[i].ownerID);
                Assert.AreEqual(state.players[owner].coreNodeID, state.villagers[i].currentNodeID);
                Assert.AreEqual(VillagerState.Idle, state.villagers[i].state);
                Assert.AreEqual(balance.baseHP, state.villagers[i].hp);
                Assert.AreEqual(-1, state.villagers[i].targetNodeID);
            }
        }

        [Test]
        public void Fill_KeepsTheCallersStateObject()
        {
            var state = new SimulationState();
            MatchFactory.Fill(state, Balance(), BoardConfigData.Default(), null, Setups());

            Assert.AreEqual(BoardConfigData.DefaultEdgeWeight, state.defaultEdgeWeight);
            Assert.AreEqual(28, state.nodes.Length);
            Assert.AreEqual(SimulationStateHasher.ComputeHash(MatchFactory.Build(Balance(), BoardConfigData.Default(), null, Setups())),
                SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void Configure_SetsPathCostsFromTheBoard()
        {
            BoardConfigData board = BoardConfigData.Default();
            board.ownedMultiplier = 11;
            board.enemyOwnedMultiplier = 222;
            try
            {
                MatchFactory.Configure(Balance(), board);
                Assert.AreEqual(11, Pathfinding.OwnedMultiplier);
                Assert.AreEqual(222, Pathfinding.EnemyOwnedMultiplier);
            }
            finally
            {
                MatchFactory.Configure(Balance(), BoardConfigData.Default());
            }
        }
    }
}
