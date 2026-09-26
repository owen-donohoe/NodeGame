using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Eras: one variant of every suit and district per arena. Two promises
    /// are pinned here. An all-era-0 match plays and hashes exactly as matches
    /// did before eras existed (the determinism baselines hold the rest of
    /// that). And a non-zero era really does play its own numbers, from the
    /// lookup through to what a tick produces.
    /// </summary>
    public class EraTests
    {
        private const int WorkNode = 1;

        private static GameBalanceData UseBalance(GameBalanceData balance)
        {
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        [TearDown]
        public void RestoreDefaultBalance()
        {
            UseBalance(GameBalanceData.Default());
        }

        private static GameBalanceData WithEra1(System.Func<DistrictStats, DistrictStats> change, DistrictType type)
        {
            GameBalanceData b = GameBalanceData.Default();
            for (int i = 0; i < b.districtStats.Length; i++)
                if (b.districtStats[i].districtType == type && b.districtStats[i].era == 1)
                    b.districtStats[i] = change(b.districtStats[i]);
            return b;
        }

        // --- Lookups ---

        [Test]
        public void DistrictLookup_ExactEra_ThenEra0_ThenZero()
        {
            GameBalanceData b = WithEra1(d => { d.productionTicks = 7; return d; }, DistrictType.Farm);

            Assert.AreEqual(7, b.GetDistrictStats(DistrictType.Farm, 1).productionTicks);
            Assert.AreEqual(30, b.GetDistrictStats(DistrictType.Farm, 0).productionTicks);

            b.districtStats = new[] { new DistrictStats { districtType = DistrictType.Farm, era = 0, productionTicks = 30 } };
            Assert.AreEqual(30, b.GetDistrictStats(DistrictType.Farm, 4).productionTicks, "a missing era falls back to era 0");
            Assert.AreEqual(0, b.GetDistrictStats(DistrictType.Mine, 0).productionTicks, "a missing district is all zero");
        }

        [Test]
        public void SuitLookup_FallsBackToEra0()
        {
            GameBalanceData b = GameBalanceData.Default();
            b.suitStats = new[]
            {
                new SuitStats { suitType = SuitType.Warrior, era = 0, attackDamage = 2 },
                new SuitStats { suitType = SuitType.Warrior, era = 2, attackDamage = 5 }
            };

            Assert.AreEqual(5, b.GetSuitStats(SuitType.Warrior, 2).attackDamage);
            Assert.AreEqual(2, b.GetSuitStats(SuitType.Warrior, 3).attackDamage);
            Assert.AreEqual(2, b.GetSuitStats(SuitType.Warrior).attackDamage);
            Assert.IsFalse(b.TryGetSuitStats(SuitType.Scout, 2, out _));
        }

        [Test]
        public void DefaultTables_CoverEveryEraIdentically()
        {
            GameBalanceData b = GameBalanceData.Default();
            foreach (DistrictType type in new[] { DistrictType.Farm, DistrictType.Market, DistrictType.Rampart,
                         DistrictType.Watchtower, DistrictType.Sanctuary, DistrictType.Shrine, DistrictType.Village })
            {
                DistrictStats era0 = b.GetDistrictStats(type, 0);
                for (int era = 1; era < GameBalanceData.EraCount; era++)
                {
                    DistrictStats other = b.GetDistrictStats(type, era);
                    Assert.AreEqual(era, other.era, type + " has no explicit era " + era);
                    other.era = 0;
                    Assert.AreEqual(era0, other, type + " era " + era + " differs from era 0");
                }
            }
        }

        [Test]
        public void WithEveryEra_CopiesEra0_AndKeepsExistingEras()
        {
            SuitStats[] suits =
            {
                new SuitStats { suitType = SuitType.Warrior, era = 0, attackDamage = 2 },
                new SuitStats { suitType = SuitType.Warrior, era = 3, attackDamage = 9 }
            };

            SuitStats[] all = GameBalanceData.WithEveryEra(suits);

            Assert.AreEqual(GameBalanceData.EraCount, all.Length);
            var b = new GameBalanceData { suitStats = all };
            Assert.AreEqual(2, b.GetSuitStats(SuitType.Warrior, 5).attackDamage);
            Assert.AreEqual(9, b.GetSuitStats(SuitType.Warrior, 3).attackDamage);
        }

        // --- Hashing ---

        [Test]
        public void Era0Fields_LeaveTheHashAsItWas_NonZeroMovesIt()
        {
            GameBalanceData balance = UseBalance(GameBalanceData.Default());
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            int before = SimulationStateHasher.ComputeHash(state);

            state.players[0].suitEras = new int[12];
            state.players[0].districtEras = new int[14];
            Assert.AreEqual(before, SimulationStateHasher.ComputeHash(state), "all-zero era tables must not move the hash");

            state.nodes[1].districtEra = 2;
            int withNode = SimulationStateHasher.ComputeHash(state);
            Assert.AreNotEqual(before, withNode);
            state.nodes[1].districtEra = 0;

            state.villagers[0].rampartBonusEra = 1;
            Assert.AreNotEqual(before, SimulationStateHasher.ComputeHash(state));
            state.villagers[0].rampartBonusEra = 0;

            state.players[0].suitEras[3] = 1;
            int suitHash = SimulationStateHasher.ComputeHash(state);
            Assert.AreNotEqual(before, suitHash);
            state.players[0].suitEras[3] = 0;

            state.players[0].districtEras[3] = 1;
            int districtHash = SimulationStateHasher.ComputeHash(state);
            Assert.AreNotEqual(before, districtHash);
            Assert.AreNotEqual(suitHash, districtHash, "a suit era and a district era at the same index must differ");
        }

        // --- Eras change what a tick does ---

        private static SimulationState BoardWithWorkerOn(GameBalanceData balance, DistrictType district, int era)
        {
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[WorkNode].districtType = district;
            state.nodes[WorkNode].baseDistrictType = district;
            state.nodes[WorkNode].districtEra = era;
            state.nodes[WorkNode].ownerID = 0;
            state.nodes[WorkNode].claimBar = balance.claimThreshold;
            state.villagers[0].currentNodeID = WorkNode;
            state.villagers[0].previousNodeID = WorkNode;
            return state;
        }

        private static void Tick(SimulationState state, int count)
        {
            for (int i = 0; i < count; i++) GameSimulation.SimulateTick(state);
        }

        [Test]
        public void FarmEra1_ProducesOnItsOwnClock()
        {
            GameBalanceData balance = UseBalance(WithEra1(d => { d.productionTicks = 10; return d; }, DistrictType.Farm));

            SimulationState era1 = BoardWithWorkerOn(balance, DistrictType.Farm, 1);
            SimulationState era0 = BoardWithWorkerOn(balance, DistrictType.Farm, 0);
            Tick(era1, 10);
            Tick(era0, 10);

            Assert.AreEqual(1, era1.players[0].food);
            Assert.AreEqual(0, era0.players[0].food);
        }

        [Test]
        public void RampartEra1_GivesAndTakesBackItsOwnBonus()
        {
            GameBalanceData balance = UseBalance(WithEra1(d => { d.maxHPBonus = 4; return d; }, DistrictType.Rampart));
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Rampart, 1);
            int baseMax = state.villagers[0].maxHP;

            Tick(state, 1);
            Assert.AreEqual(baseMax + 4, state.villagers[0].maxHP);
            Assert.AreEqual(1, state.villagers[0].rampartBonusEra);

            // Era 1's numbers change under it: leaving must still take back
            // what arriving gave, read from the era the bonus came from.
            state.nodes[WorkNode].districtType = DistrictType.None;
            Tick(state, 1);
            Assert.AreEqual(baseMax, state.villagers[0].maxHP);
            Assert.IsFalse(state.villagers[0].hasRampartBonus);
            Assert.AreEqual(0, state.villagers[0].rampartBonusEra);
        }

        [Test]
        public void DraftedDistrict_TakesItsPlacersEra_AndVillageBonusFollows()
        {
            GameBalanceData balance = WithEra1(d => { d.bonusVillagersOnClaim = 5; return d; }, DistrictType.Village);
            var eras = new int[14];
            eras[(int)DistrictType.Village] = 1;
            PlayerSetup[] players =
            {
                new PlayerSetup { suits = new int[0], nodes = new int[0], districtEras = eras },
                new PlayerSetup { suits = new int[0], nodes = new int[0] }
            };
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Village, gridX = 0, gridZ = 3 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 3, gridZ = 3 }
            };

            SimulationState state = MatchFactory.Build(balance, BoardConfigData.Default(), draft, players);

            Assert.AreEqual(1, state.nodes[12].districtEra);
            Assert.AreEqual(5, state.nodes[12].bonusVillagersOnClaim);
            Assert.AreEqual(0, state.nodes[15].districtEra);
            Assert.AreEqual(2, state.nodes[15].bonusVillagersOnClaim);
            Assert.AreEqual(eras, state.players[0].districtEras);
        }

        [Test]
        public void EquippingASuit_UsesThePlayersEraOfIt()
        {
            GameBalanceData balance = GameBalanceData.Default();
            balance.suitStats = new[]
            {
                new SuitStats { suitType = SuitType.Warrior, era = 0, attackDamage = 2, moveSpeedTicks = 4, attackCooldownMax = 20 },
                new SuitStats { suitType = SuitType.Warrior, era = 1, attackDamage = 6, moveSpeedTicks = 4, attackCooldownMax = 20 }
            };
            UseBalance(balance);

            int Equip(int era)
            {
                SimulationState state = BoardWithWorkerOn(balance, DistrictType.Barracks, 0);
                state.players[0].draftedSuits = new[] { (int)SuitType.Warrior };
                state.players[0].suitEras = new int[12];
                state.players[0].suitEras[(int)SuitType.Warrior] = era;
                CommandProcessor.ProcessCommand(state, new GameCommand
                {
                    type = CommandType.Equip, playerID = 0, villagerID = 0, value = (int)SuitType.Warrior
                });
                Assert.AreEqual(SuitType.Warrior, state.villagers[0].suit, "the equip must apply");
                return state.villagers[0].attackDamage;
            }

            Assert.AreEqual(6, Equip(1));
            Assert.AreEqual(2, Equip(0));
        }
    }
}
