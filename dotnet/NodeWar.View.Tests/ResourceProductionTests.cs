using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.UI;

namespace NodeWar.View.Tests
{
    /// <summary>
    /// What the HUD rings paint dim white: the player's production jobs in
    /// flight for one resource. These cover the rule for what counts -
    /// TickProduction's, read back rather than guessed - and the ordering the
    /// rings rely on, nearest to landing first.
    /// </summary>
    public class ResourceProductionTests
    {
        private const int FoodTicks = 30;
        private const int MarketFoodTicks = 45;
        private const int MarketMaterialTicks = 60;

        private static GameBalanceData Balance()
        {
            return new GameBalanceData
            {
                districtStats = new[]
                {
                    new DistrictStats { districtType = DistrictType.Market, productionTicks = MarketFoodTicks,
                        secondaryProductionTicks = MarketMaterialTicks }
                }
            };
        }

        /// <summary>
        /// A board of one node per district, so a villager can be put on any
        /// of them by index. Node 0 Farm, 1 Mine, 2 Forge, 3 Market.
        /// </summary>
        private static SimulationState State(int materials = 5, int allocation = 1)
        {
            return new SimulationState
            {
                nodes = new[]
                {
                    new NodeData { nodeID = 0, districtType = DistrictType.Farm },
                    new NodeData { nodeID = 1, districtType = DistrictType.Mine },
                    new NodeData { nodeID = 2, districtType = DistrictType.Forge, materialAllocation = allocation },
                    new NodeData { nodeID = 3, districtType = DistrictType.Market }
                },
                players = new[]
                {
                    new PlayerData { playerID = 0, materials = materials },
                    new PlayerData { playerID = 1, materials = materials }
                },
                villagers = new VillagerData[0]
            };
        }

        private static VillagerData Worker(int id, int owner, int nodeID, int remaining, int max)
        {
            return new VillagerData
            {
                villagerID = id,
                ownerID = owner,
                currentNodeID = nodeID,
                state = VillagerState.Working,
                productionTicksRemaining = remaining,
                productionTicksMax = max
            };
        }

        // ===== progress, the same reading ProductionContent takes =====

        [TestCase(30, 30, 0f)]
        [TestCase(15, 30, 0.5f)]
        [TestCase(6, 30, 0.8f)]
        [TestCase(1, 30, 29f / 30f)]
        public void Progress_runs_from_zero_at_the_start_to_one_at_the_end(
            int remaining, int max, float expected)
        {
            VillagerData villager = Worker(0, 0, 0, remaining, max);
            Assert.AreEqual(expected, ResourceProduction.Progress(villager, 0f), 1e-5f);
        }

        [Test]
        public void Progress_carries_the_tick_alpha_so_the_fill_moves_between_ticks()
        {
            VillagerData villager = Worker(0, 0, 0, 15, 30);

            float atTick = ResourceProduction.Progress(villager, 0f);
            float halfway = ResourceProduction.Progress(villager, 0.5f);

            Assert.Greater(halfway, atTick);
            Assert.AreEqual(0.5f + 0.5f / 30f, halfway, 1e-5f);
        }

        [Test]
        public void Progress_is_zero_for_a_villager_with_no_timer()
        {
            Assert.AreEqual(0f, ResourceProduction.Progress(Worker(0, 0, 0, 0, 0), 0.5f));
        }

        [Test]
        public void Progress_never_leaves_zero_to_one()
        {
            // A full alpha on the last tick would otherwise overshoot.
            Assert.AreEqual(1f, ResourceProduction.Progress(Worker(0, 0, 0, 0, 30), 1f));
            Assert.AreEqual(0f, ResourceProduction.Progress(Worker(0, 0, 0, 40, 30), 0f));
        }

        // ===== the worked example from the ask =====

        [Test]
        public void Two_farms_at_half_report_two_jobs_at_half()
        {
            SimulationState state = State();
            state.villagers = new[]
            {
                Worker(0, 0, 0, FoodTicks / 2, FoodTicks),
                Worker(1, 0, 0, FoodTicks / 2, FoodTicks)
            };

            float[] into = new float[ResourceProduction.MaxInFlight];
            int count = ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into);

            Assert.AreEqual(2, count);
            Assert.AreEqual(0.5f, into[0], 1e-5f);
            Assert.AreEqual(0.5f, into[1], 1e-5f);
        }

        // ===== nearest to landing first =====

        [Test]
        public void Jobs_come_back_sorted_with_the_nearest_to_landing_first()
        {
            SimulationState state = State();
            state.villagers = new[]
            {
                Worker(0, 0, 0, 24, FoodTicks), // 20%
                Worker(1, 0, 0, 3, FoodTicks),  // 90%
                Worker(2, 0, 0, 15, FoodTicks)  // 50%
            };

            float[] into = new float[ResourceProduction.MaxInFlight];
            int count = ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into);

            Assert.AreEqual(3, count);
            Assert.AreEqual(0.9f, into[0], 1e-5f);
            Assert.AreEqual(0.5f, into[1], 1e-5f);
            Assert.AreEqual(0.2f, into[2], 1e-5f);
        }

        [Test]
        public void Equal_jobs_keep_villager_order_rather_than_swapping()
        {
            SimulationState state = State();
            state.villagers = new[]
            {
                Worker(0, 0, 0, 15, FoodTicks),
                Worker(1, 0, 0, 15, FoodTicks),
                Worker(2, 0, 0, 15, FoodTicks)
            };

            float[] into = new float[ResourceProduction.MaxInFlight];
            int first = ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into);

            float[] again = new float[ResourceProduction.MaxInFlight];
            int second = ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, again);

            Assert.AreEqual(first, second);
            for (int i = 0; i < first; i++) Assert.AreEqual(into[i], again[i]);
        }

        // ===== what counts, and what does not =====

        [Test]
        public void A_villager_that_is_not_working_is_not_in_flight()
        {
            SimulationState state = State();
            VillagerData walker = Worker(0, 0, 0, 15, FoodTicks);
            walker.state = VillagerState.Moving;
            state.villagers = new[] { walker };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
        }

        [Test]
        public void A_consumed_villager_is_not_in_flight()
        {
            SimulationState state = State();
            VillagerData spent = Worker(0, 0, 0, 15, FoodTicks);
            spent.isConsumed = true;
            state.villagers = new[] { spent };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
        }

        [Test]
        public void The_other_players_production_is_not_reported()
        {
            SimulationState state = State();
            state.villagers = new[] { Worker(0, 1, 0, 15, FoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
            Assert.AreEqual(1, ResourceProduction.InFlight(state, Balance(), 1, ResourceKind.Food, 0f, into));
        }

        [Test]
        public void A_mine_counts_toward_materials_and_not_food()
        {
            SimulationState state = State();
            state.villagers = new[] { Worker(0, 0, 1, 15, FoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
            Assert.AreEqual(1, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Materials, 0f, into));
        }

        // ===== the forge, the one that can run and yield nothing =====

        [Test]
        public void A_forge_with_allocation_and_materials_is_in_flight_for_metal()
        {
            SimulationState state = State(materials: 3, allocation: 1);
            state.villagers = new[] { Worker(0, 0, 2, 15, FoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(1, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Metal, 0f, into));
        }

        [Test]
        public void A_forge_with_no_allocation_shows_nothing()
        {
            SimulationState state = State(materials: 3, allocation: 0);
            state.villagers = new[] { Worker(0, 0, 2, 15, FoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Metal, 0f, into));
        }

        [Test]
        public void A_forge_that_cannot_pay_its_material_shows_nothing()
        {
            SimulationState state = State(materials: 0, allocation: 1);
            state.villagers = new[] { Worker(0, 0, 2, 15, FoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Metal, 0f, into));
        }

        // ===== the market alternates, and says which half by its timer =====

        [Test]
        public void A_market_on_its_food_half_counts_toward_food()
        {
            SimulationState state = State();
            state.villagers = new[] { Worker(0, 0, 3, 20, MarketFoodTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(1, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Materials, 0f, into));
        }

        [Test]
        public void A_market_on_its_material_half_counts_toward_materials()
        {
            SimulationState state = State();
            state.villagers = new[] { Worker(0, 0, 3, 20, MarketMaterialTicks) };

            float[] into = new float[ResourceProduction.MaxInFlight];
            Assert.AreEqual(0, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into));
            Assert.AreEqual(1, ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Materials, 0f, into));
        }

        // ===== the buffer is never overrun =====

        [Test]
        public void More_jobs_than_the_buffer_holds_fill_it_with_the_nearest_ones()
        {
            SimulationState state = State();
            state.villagers = new VillagerData[6];
            for (int i = 0; i < state.villagers.Length; i++)
            {
                // Villager i is (i+1)/10 of the way through: later ones are further along.
                state.villagers[i] = Worker(i, 0, 0, FoodTicks - (i + 1) * 3, FoodTicks);
            }

            float[] into = new float[3];
            int count = ResourceProduction.InFlight(state, Balance(), 0, ResourceKind.Food, 0f, into);

            Assert.AreEqual(3, count);
            Assert.AreEqual(0.6f, into[0], 1e-5f);
            Assert.AreEqual(0.5f, into[1], 1e-5f);
            Assert.AreEqual(0.4f, into[2], 1e-5f);
        }

        [Test]
        public void An_empty_or_null_state_reports_nothing_rather_than_throwing()
        {
            float[] into = new float[ResourceProduction.MaxInFlight];

            Assert.AreEqual(0, ResourceProduction.InFlight(null, Balance(), 0, ResourceKind.Food, 0f, into));
            Assert.AreEqual(0, ResourceProduction.InFlight(State(), Balance(), 0, ResourceKind.Food, 0f, null));
            Assert.AreEqual(0, ResourceProduction.InFlight(State(), Balance(), 0, ResourceKind.Food, 0f, new float[0]));
        }
    }
}
