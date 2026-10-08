using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// The starting board every match, replay and headless run begins from.
    /// These pin what GameManager built by hand before MatchFactory existed;
    /// a change here changes every match, so it is a SimulationVersion bump.
    /// They run on an explicit 3x3 land board, where a node's ID is its cell
    /// index; the shipped map is covered by TerrainMapTests.
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
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior, (int)SuitType.Scout }, districts = new[] { (int)DistrictType.Rampart } },
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] }
            };
        }

        [Test]
        public void LandBoard_CoresBelongToTheirPlayersByPosition()
        {
            GameBalanceData balance = Balance();
            SimulationState state = MatchFactory.Build(balance, BoardFixtures.LandGrid3x3(), null, Setups());

            Assert.AreEqual(9, state.nodes.Length);
            Assert.AreEqual(BoardFixtures.P0Core3x3, state.players[0].coreNodeID, "P0 owns the highest-Z core (1,2).");
            Assert.AreEqual(BoardFixtures.P1Core3x3, state.players[1].coreNodeID, "P1 owns the lowest-Z core (1,0).");
            Assert.AreEqual(0, state.nodes[7].ownerID);
            Assert.AreEqual(balance.claimThreshold, state.nodes[7].claimBar);
            Assert.AreEqual(1, state.nodes[1].ownerID);
            Assert.AreEqual(-balance.claimThreshold, state.nodes[1].claimBar);
            Assert.AreEqual(DistrictType.Core, state.nodes[7].districtType);
            Assert.AreEqual(DistrictType.None, state.nodes[0].districtType);
            Assert.AreEqual(-1, state.nodes[0].ownerID);
            Assert.AreEqual(TerrainType.Land, state.nodes[0].terrain);
        }

        [Test]
        public void MisconfiguredCoreOwners_AreCorrected()
        {
            BoardConfigData board = BoardFixtures.LandGrid3x3();
            board.initialPlacements[0].ownerID = 1; // (1,2) claims to be P1's
            board.initialPlacements[1].ownerID = 0;

            SimulationState state = MatchFactory.Build(Balance(), board, null, Setups());

            Assert.AreEqual(0, state.nodes[7].ownerID);
            Assert.AreEqual(1, state.nodes[1].ownerID);
        }

        [Test]
        public void Links_AreLeftRightDownUp()
        {
            SimulationState state = MatchFactory.Build(Balance(), BoardFixtures.LandGrid3x3(), null, Setups());

            Link[] middle = state.nodes[4].links; // (1,1)
            Assert.AreEqual(new[] { 3, 5, 1, 7 }, new[] { middle[0].toNodeID, middle[1].toNodeID, middle[2].toNodeID, middle[3].toNodeID });
            Link[] corner = state.nodes[0].links;
            Assert.AreEqual(2, corner.Length);
            Assert.AreEqual(1, corner[0].toNodeID);
            Assert.AreEqual(3, corner[1].toNodeID);
            Assert.AreEqual(BoardConfigData.DefaultLinkWeight, corner[0].travelWeight);
        }

        [Test]
        public void DraftPlacements_AreUnownedAndVillagesCarryTheirBonus()
        {
            GameBalanceData balance = Balance();
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Village, gridX = 0, gridZ = 1 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 2, gridZ = 1 }
            };

            SimulationState state = MatchFactory.Build(balance, BoardFixtures.LandGrid3x3(), draft, Setups());

            Assert.AreEqual(DistrictType.Village, state.nodes[3].districtType);
            Assert.AreEqual(DistrictType.Village, state.nodes[3].baseDistrictType);
            Assert.AreEqual(-1, state.nodes[3].ownerID);
            Assert.AreEqual(0, state.nodes[3].townPaidMask);
            Assert.AreEqual(DistrictType.Farm, state.nodes[5].districtType);
            Assert.AreEqual(0, state.nodes[5].townPaidMask);
        }

        [Test]
        public void Players_GetStartingResourcesAndTheirDraftedTypes()
        {
            BoardConfigData board = BoardFixtures.LandGrid3x3();
            board.startingFood = 3;
            board.startingMaterials = 4;
            board.startingMetal = 5;

            SimulationState state = MatchFactory.Build(Balance(), board, null, Setups());

            Assert.AreEqual(3, state.players[1].food);
            Assert.AreEqual(4, state.players[1].materials);
            Assert.AreEqual(5, state.players[1].metal);
            Assert.AreEqual(new[] { (int)SuitType.Warrior, (int)SuitType.Scout }, state.players[0].draftedSuits);
            Assert.AreEqual(new[] { (int)DistrictType.Rampart }, state.players[0].draftedDistricts);
            Assert.AreEqual(new int[0], state.players[1].draftedDistricts);
        }

        [Test]
        public void Villagers_StartIdleOnTheirCore_P0First()
        {
            GameBalanceData balance = Balance();
            SimulationState state = MatchFactory.Build(balance, BoardFixtures.LandGrid3x3(), null, Setups());

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
            MatchFactory.Fill(state, Balance(), BoardFixtures.LandGrid3x3(), null, Setups());

            Assert.AreEqual(BoardConfigData.DefaultLinkWeight, state.defaultLinkWeight);
            Assert.AreEqual(9, state.nodes.Length);
            Assert.AreEqual(BoardHasher.Hash(BoardFixtures.LandGrid3x3()), state.boardHash);
            Assert.AreEqual(SimulationStateHasher.ComputeHash(MatchFactory.Build(Balance(), BoardFixtures.LandGrid3x3(), null, Setups())),
                SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void Configure_SetsPathCostsFromTheBoard()
        {
            BoardConfigData board = BoardFixtures.LandGrid3x3();
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
                MatchFactory.Configure(Balance(), BoardFixtures.LandGrid3x3());
            }
        }
    }
}
