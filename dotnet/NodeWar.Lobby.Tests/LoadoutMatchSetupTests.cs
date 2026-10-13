using System;
using System.Linq;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class LoadoutMatchSetupTests
    {
        private static LoadoutData Low() => new LoadoutData
        {
            suitIDs = new[] { "suit_guardian", "suit_scout" },
            districtIDs = new[] { "node_barracks", "node_forge" }
        };

        private static LoadoutData High(bool warrior = false) => new LoadoutData
        {
            suitIDs = new[] { "suit_guardian", warrior ? "suit_warrior" : "suit_medic", "suit_scout" },
            districtIDs = new[] { "node_barracks", "node_forge", "node_farm", "node_mine", "node_village" }
        };

        [Test]
        public void LowPool_BasePlusTwo()
        {
            var board = PremadeMaps.Hourglass01();
            var pool = LoadoutMatchSetup.DistrictPool(Low(), board.baseDraftDistrictsP0);
            Assert.IsTrue(LoadoutMatchSetup.GetsFreeGrants(Low()));
            CollectionAssert.AreEqual(new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village,
                DistrictType.Barracks, DistrictType.Forge }, pool.Select(p => p.DistrictType));
            CollectionAssert.AreEqual(new[] { false, false, false, true, true }, pool.Select(p => p.IsFromLoadout));
            CollectionAssert.AreEqual(new[] { (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Scout },
                LoadoutMatchSetup.DraftedSuits(Low()));
            CollectionAssert.AreEqual(new[] { (int)DistrictType.Barracks, (int)DistrictType.Forge },
                LoadoutMatchSetup.DraftedDistricts(Low()));
        }

        [Test]
        public void HighPool_FiveChosenOnly()
        {
            var pool = LoadoutMatchSetup.DistrictPool(High(), PremadeMaps.Hourglass01().baseDraftDistrictsP0);
            Assert.IsFalse(LoadoutMatchSetup.GetsFreeGrants(High()));
            CollectionAssert.AreEqual(LoadoutMatchSetup.DraftedDistricts(High()), pool.Select(p => (int)p.DistrictType));
            Assert.AreEqual(5, pool.Length);
            Assert.IsTrue(pool.All(p => p.IsFromLoadout));
            Assert.AreEqual(3, LoadoutMatchSetup.DraftedSuits(High()).Length);
        }

        [Test]
        public void HighWarrior_SelectedOnceOrAbsent()
        {
            CollectionAssert.AreEqual(new[] { (int)SuitType.Guardian, (int)SuitType.Warrior, (int)SuitType.Scout },
                LoadoutMatchSetup.DraftedSuits(High(true)));
            CollectionAssert.DoesNotContain(LoadoutMatchSetup.DraftedSuits(High()), (int)SuitType.Warrior);
            var low = Low();
            low.suitIDs[0] = "suit_warrior";
            CollectionAssert.AreEqual(new[] { (int)SuitType.Warrior, (int)SuitType.Scout },
                LoadoutMatchSetup.DraftedSuits(low));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MixedModes_IndependentBothSeats(int highSeat)
        {
            var board = PremadeMaps.Hourglass01();
            for (int seat = 0; seat < 2; seat++)
            {
                var loadout = seat == highSeat ? High() : Low();
                var pool = LoadoutMatchSetup.DistrictPool(loadout,
                    seat == 0 ? board.baseDraftDistrictsP0 : board.baseDraftDistrictsP1);
                Assert.AreEqual(5, pool.Length);
                Assert.AreEqual(seat != highSeat, pool.Any(p => !p.IsFromLoadout));
                Assert.AreEqual(seat != highSeat, LoadoutMatchSetup.DraftedSuits(loadout).Contains((int)SuitType.Warrior));
            }
        }

        [Test]
        public void BlankSlots_DoNotChangeMode()
        {
            var board = PremadeMaps.Hourglass01();
            Assert.AreEqual(0, LoadoutMatchSetup.DistrictPool(LoadoutData.CreateEmpty(2), board.baseDraftDistrictsP0).Length);
            Assert.AreEqual(0, LoadoutMatchSetup.DraftedSuits(LoadoutData.CreateEmpty(2)).Length);
            Assert.AreEqual(3, LoadoutMatchSetup.DistrictPool(LoadoutData.CreateEmpty(1), board.baseDraftDistrictsP0).Length);
            CollectionAssert.AreEqual(new[] { (int)SuitType.Warrior }, LoadoutMatchSetup.DraftedSuits(LoadoutData.CreateEmpty(1)));
        }

        [Test]
        public void InputBoardPoolsAndHash_Unchanged()
        {
            var board = PremadeMaps.Hourglass01();
            var hash = BoardHasher.Hash(board);
            var p0 = (DistrictType[])board.baseDraftDistrictsP0.Clone();
            var p1 = (DistrictType[])board.baseDraftDistrictsP1.Clone();
            var low = Low();
            var suits = (string[])low.suitIDs.Clone();
            var districts = (string[])low.districtIDs.Clone();
            LoadoutMatchSetup.DistrictPool(High(), board.baseDraftDistrictsP0);
            LoadoutMatchSetup.DistrictPool(low, board.baseDraftDistrictsP1);
            LoadoutMatchSetup.DraftedSuits(low);
            LoadoutMatchSetup.DraftedDistricts(low);
            Assert.AreEqual(hash, BoardHasher.Hash(board));
            CollectionAssert.AreEqual(p0, board.baseDraftDistrictsP0);
            CollectionAssert.AreEqual(p1, board.baseDraftDistrictsP1);
            CollectionAssert.AreEqual(suits, low.suitIDs);
            CollectionAssert.AreEqual(districts, low.districtIDs);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MixedSetup_MatchFactoryKeepsInputsAndStartsUnsuited(int highSeat)
        {
            var board = PremadeMaps.Hourglass01();
            var balance = GameBalanceData.Default();
            var setups = new PlayerSetup[2];
            for (int seat = 0; seat < 2; seat++)
            {
                var loadout = seat == highSeat ? High() : Low();
                setups[seat] = new PlayerSetup { suits = LoadoutMatchSetup.DraftedSuits(loadout),
                    districts = LoadoutMatchSetup.DraftedDistricts(loadout) };
            }
            var draft = new[] { new DraftPlacement { playerID = 0, gridX = 2, gridZ = 5, districtType = DistrictType.Barracks },
                new DraftPlacement { playerID = 1, gridX = 2, gridZ = 1, districtType = DistrictType.Barracks } };
            var first = MatchFactory.Build(balance, board, draft, setups);
            var second = MatchFactory.Build(balance, board, draft, setups);
            for (int seat = 0; seat < 2; seat++)
            {
                CollectionAssert.AreEqual(setups[seat].suits, first.players[seat].draftedSuits);
                CollectionAssert.AreEqual(setups[seat].districts, first.players[seat].draftedDistricts);
                Assert.AreEqual(DistrictType.Core, first.nodes[first.players[seat].coreNodeID].districtType);
            }
            Assert.Greater(first.villagers.Length, 0);
            Assert.IsTrue(first.villagers.All(v => v.suit == SuitType.None));
            Assert.AreEqual(SimulationStateHasher.ComputeHash(first), SimulationStateHasher.ComputeHash(second));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Commands_OmittedWarriorRefused_AlternativeEquips_RespawnUnsuited(int seat)
        {
            var balance = GameBalanceData.Default();
            balance.suitStats = new[] { new SuitStats { suitType = SuitType.Warrior },
                new SuitStats { suitType = SuitType.Guardian } };
            var board = PremadeMaps.Hourglass01();
            // Only commands need static balance; leave simulation/pathfinding globals alone.
            var balanceField = typeof(CommandProcessor).GetField("bal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var previousBalance = (GameBalanceData)balanceField.GetValue(null);
            var setups = new[] { new PlayerSetup { suits = LoadoutMatchSetup.DraftedSuits(High()) },
                new PlayerSetup { suits = LoadoutMatchSetup.DraftedSuits(High()) } };
            var draft = new[] {
                new DraftPlacement { playerID = seat, gridX = 2, gridZ = seat == 0 ? 5 : 1, districtType = DistrictType.Barracks }
            };
            var state = MatchFactory.Build(balance, board, draft, setups);
            int vid = Array.FindIndex(state.villagers, v => v.ownerID == seat);
            int barracks = MatchFactory.CellToNode(board, draft)[draft[0].gridZ * board.gridCols + draft[0].gridX];
            Assert.GreaterOrEqual(vid, 0);
            Assert.GreaterOrEqual(barracks, 0);
            // Drafted districts start neutral. Model a claimed Barracks before equipping.
            Assert.AreEqual(-1, state.nodes[barracks].ownerID);
            state.nodes[barracks].ownerID = seat;
            state.nodes[barracks].claimBar = seat == 0 ? balance.claimThreshold : -balance.claimThreshold;
            state.villagers[vid].currentNodeID = barracks;
            state.villagers[vid].state = VillagerState.Idle;
            state.players[seat].food = 1000;
            state.players[seat].materials = 1000;
            try
            {
                CommandProcessor.SetBalance(balance);
                int hash = SimulationStateHasher.ComputeHash(state);
                CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Equip,
                    playerID = seat, villagerID = vid, value = (int)SuitType.Warrior });
                Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(state));
                CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Equip,
                    playerID = seat, villagerID = vid, value = (int)SuitType.Guardian });
                Assert.AreEqual(SuitType.Guardian, state.villagers[vid].suit);
                state.villagers[vid].state = VillagerState.Dead;
                CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn,
                    playerID = seat, villagerID = vid });
                Assert.AreEqual(SuitType.None, state.villagers[vid].suit);
                Assert.AreEqual(state.players[seat].coreNodeID, state.villagers[vid].currentNodeID);
                Assert.AreEqual(1, state.players[seat].paidRespawns);
            }
            finally
            {
                CommandProcessor.SetBalance(previousBalance);
            }
        }

        [TestCase("shape")]
        [TestCase("unknownSuit")]
        [TestCase("unknownDistrict")]
        [TestCase("duplicateSuit")]
        [TestCase("duplicateDistrict")]
        [TestCase("core")]
        [TestCase("retiredDistrict")]
        [TestCase("nullShape")]
        [TestCase("nullSuits")]
        [TestCase("nullDistricts")]
        public void InvalidChoices_SanitizeAtMatchBoundary(string invalid)
        {
            var loadout = High();
            var suits = new[] { (int)SuitType.Guardian, (int)SuitType.Medic, (int)SuitType.Scout };
            var districts = new[] { (int)DistrictType.Barracks, (int)DistrictType.Forge,
                (int)DistrictType.Farm, (int)DistrictType.Mine, (int)DistrictType.Village };
            bool low = false;
            switch (invalid)
            {
                case "shape":
                    loadout.districtIDs = new string[2];
                    low = true;
                    suits = new[] { (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Medic };
                    districts = Array.Empty<int>();
                    break;
                case "unknownSuit":
                    loadout.suitIDs[0] = "suit_unknown";
                    suits = suits.Skip(1).ToArray();
                    break;
                case "unknownDistrict":
                    loadout.districtIDs[0] = "node_unknown";
                    districts = districts.Skip(1).ToArray();
                    break;
                case "duplicateSuit":
                    loadout.suitIDs[1] = "suit.guardian";
                    suits = new[] { (int)SuitType.Guardian, (int)SuitType.Scout };
                    break;
                case "duplicateDistrict":
                    loadout.districtIDs[1] = "district.barracks";
                    districts = new[] { (int)DistrictType.Barracks, (int)DistrictType.Farm,
                        (int)DistrictType.Mine, (int)DistrictType.Village };
                    break;
                case "core":
                    loadout.districtIDs[0] = "node_core";
                    districts = districts.Skip(1).ToArray();
                    break;
                case "retiredDistrict":
                    loadout.districtIDs[0] = "node_camp";
                    districts = districts.Skip(1).ToArray();
                    break;
                case "nullShape":
                    loadout = default;
                    low = true;
                    suits = new[] { (int)SuitType.Warrior };
                    districts = Array.Empty<int>();
                    break;
                case "nullSuits":
                    loadout.suitIDs = null;
                    low = true;
                    suits = new[] { (int)SuitType.Warrior };
                    districts = districts.Take(2).ToArray();
                    break;
                case "nullDistricts":
                    loadout.districtIDs = null;
                    low = true;
                    suits = new[] { (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Medic };
                    districts = Array.Empty<int>();
                    break;
            }
            var originalSuits = loadout.suitIDs?.ToArray();
            var originalDistricts = loadout.districtIDs?.ToArray();
            Assert.AreEqual(low, LoadoutMatchSetup.GetsFreeGrants(loadout));
            var pool = LoadoutMatchSetup.DistrictPool(loadout, null);
            CollectionAssert.AreEqual(districts, pool.Select(p => (int)p.DistrictType));
            Assert.IsTrue(pool.All(p => p.IsFromLoadout));
            CollectionAssert.AreEqual(suits, LoadoutMatchSetup.DraftedSuits(loadout));
            CollectionAssert.AreEqual(districts, LoadoutMatchSetup.DraftedDistricts(loadout));
            CollectionAssert.AreEqual(suits, LoadoutMatchSetup.DraftedSuits(loadout));
            CollectionAssert.AreEqual(originalSuits, loadout.suitIDs);
            CollectionAssert.AreEqual(originalDistricts, loadout.districtIDs);
        }

        [Test]
        public void DefaultLoadout_LowMapPoolAndWarrior()
        {
            LoadoutData loadout = default;
            var basePool = PremadeMaps.Hourglass01().baseDraftDistrictsP0;
            var pool = LoadoutMatchSetup.DistrictPool(loadout, basePool);
            Assert.IsTrue(LoadoutMatchSetup.GetsFreeGrants(loadout));
            CollectionAssert.AreEqual(basePool, pool.Select(p => p.DistrictType));
            Assert.IsTrue(pool.All(p => !p.IsFromLoadout));
            CollectionAssert.AreEqual(new[] { (int)SuitType.Warrior }, LoadoutMatchSetup.DraftedSuits(loadout));
            Assert.IsEmpty(LoadoutMatchSetup.DraftedDistricts(loadout));
        }
    }
}
