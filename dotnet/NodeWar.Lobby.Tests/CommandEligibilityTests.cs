using System.Collections.Generic;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.UI;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// CommandEligibility held against CommandProcessor itself.
    ///
    /// The in-match sheet enables a button, names a refusal, or shows a price
    /// from CommandEligibility. If that answer disagrees with what the
    /// simulation does, the player sees a button that silently does nothing,
    /// or pays a price they were not shown. So these tests do not restate the
    /// rules a second time - they run the real command on a copy of the state
    /// and require that "accepted" means "the state changed", for every
    /// combination on a small board.
    /// </summary>
    [TestFixture]
    public class CommandEligibilityTests
    {
        private const int Core0 = 0, Barracks0 = 1, Sanctuary0 = 2, Barracks1 = 3, Forge0 = 4, Forge1 = 5, Camp0 = 6, Farm0 = 7, Sanctuary1 = 8;

        private static GameBalanceData Balance()
        {
            GameBalanceData b = GameBalanceData.Default();
            b.respawnCostFood = 8;
            b.suitStats = new[]
            {
                new SuitStats { suitType = SuitType.Warrior, foodCost = 1, materialCost = 1, attackDamage = 2, moveSpeedTicks = 4, attackCooldownMax = 20 },
                new SuitStats { suitType = SuitType.Guardian, foodCost = 2, materialCost = 1, bonusHP = 3, attackDamage = 1, moveSpeedTicks = 5, attackCooldownMax = 20 },
                new SuitStats { suitType = SuitType.Scout, foodCost = 1, materialCost = 0, attackDamage = 1, moveSpeedTicks = 2, attackCooldownMax = 20 },
                new SuitStats { suitType = SuitType.Berserker, foodCost = 2, materialCost = 2, attackDamage = 3, moveSpeedTicks = 4, attackCooldownMax = 15 },
                new SuitStats { suitType = SuitType.Medic, foodCost = 1, materialCost = 2, attackDamage = 0, moveSpeedTicks = 4, attackCooldownMax = 20 },
            };
            return b;
        }

        private static NodeData Node(int id, DistrictType district, int owner)
        {
            return new NodeData { nodeID = id, districtType = district, ownerID = owner, edges = new Edge[0] };
        }

        private static VillagerData Villager(int id, int owner, int node, VillagerState state,
                                             SuitType suit = SuitType.None, bool consumed = false, int respawn = 0)
        {
            return new VillagerData
            {
                villagerID = id, ownerID = owner, currentNodeID = node, previousNodeID = node,
                targetNodeID = -1, movePath = new int[0], state = state, suit = suit,
                isConsumed = consumed, respawnTicksRemaining = respawn, hp = 5, maxHP = 5, combatTargetID = -1
            };
        }

        /// <summary>
        /// Nine nodes and a villager in every situation the checks care about:
        /// idle on each kind of district, busy, already suited, dead, consumed,
        /// the opponent's, and working a Sanctuary on either side's ground.
        /// </summary>
        private static SimulationState Board(int food, int materials, int[] drafted)
        {
            SimulationState s = new SimulationState();
            s.nodes = new[]
            {
                Node(Core0, DistrictType.Core, 0), Node(Barracks0, DistrictType.Barracks, 0),
                Node(Sanctuary0, DistrictType.Sanctuary, 0), Node(Barracks1, DistrictType.Barracks, 1),
                Node(Forge0, DistrictType.Forge, 0), Node(Forge1, DistrictType.Forge, 1),
                Node(Camp0, DistrictType.Camp, 0), Node(Farm0, DistrictType.Farm, 0),
                Node(Sanctuary1, DistrictType.Sanctuary, 1),
            };
            s.villagers = new[]
            {
                Villager(0, 0, Barracks0, VillagerState.Idle),
                Villager(1, 0, Sanctuary0, VillagerState.Idle),
                Villager(2, 0, Camp0, VillagerState.Idle),
                Villager(3, 0, Farm0, VillagerState.Idle),
                Villager(4, 0, Barracks1, VillagerState.Idle),
                Villager(5, 0, Barracks0, VillagerState.Moving),
                Villager(6, 0, Barracks0, VillagerState.Working),
                Villager(7, 0, Barracks0, VillagerState.Idle, SuitType.Warrior),
                Villager(8, 0, Barracks0, VillagerState.Idle, SuitType.Farmer),
                Villager(9, 0, Core0, VillagerState.Dead, respawn: 30),
                Villager(10, 0, Core0, VillagerState.Dead, respawn: 12),
                Villager(11, 0, Core0, VillagerState.Dead, consumed: true, respawn: 40),
                Villager(12, 1, Barracks1, VillagerState.Idle),
                Villager(13, 1, Core0, VillagerState.Dead, respawn: 45),
                Villager(14, 0, Sanctuary0, VillagerState.Working, SuitType.Acolyte),
                Villager(15, 0, Sanctuary1, VillagerState.Working, SuitType.Acolyte),
                Villager(16, 0, Barracks0, VillagerState.Idle, consumed: true),
            };
            s.players = new[]
            {
                new PlayerData { playerID = 0, coreNodeID = Core0, food = food, materials = materials, draftedSuits = drafted },
                new PlayerData { playerID = 1, coreNodeID = Barracks1, food = food, materials = materials, draftedSuits = drafted },
            };
            return s;
        }

        private static SimulationState Clone(SimulationState s)
        {
            SimulationState c = new SimulationState();
            c.nodes = (NodeData[])s.nodes.Clone();
            c.villagers = (VillagerData[])s.villagers.Clone();
            c.players = (PlayerData[])s.players.Clone();
            c.tickCount = s.tickCount;
            return c;
        }

        private static IEnumerable<SimulationState> Boards()
        {
            int[] all = { (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Scout, (int)SuitType.Berserker, (int)SuitType.Medic };
            int[] some = { (int)SuitType.Warrior, (int)SuitType.Medic };
            foreach (int[] drafted in new[] { all, some, null })
                foreach (int food in new[] { 0, 1, 2, 5, 8, 20 })
                    foreach (int materials in new[] { 0, 1, 2 })
                        yield return Board(food, materials, drafted);
        }

        [SetUp]
        public void SetUp()
        {
            CommandProcessor.SetBalance(Balance());
        }

        // ===== EQUIP =====

        [Test]
        public void Equip_AcceptedExactlyWhenCommandProcessorEquips()
        {
            GameBalanceData balance = Balance();
            System.Array suits = System.Enum.GetValues(typeof(SuitType));
            int checkedCases = 0, accepted = 0;

            foreach (SimulationState board in Boards())
                for (int player = 0; player < 2; player++)
                    for (int vid = -1; vid <= board.villagers.Length; vid++)
                        foreach (SuitType suit in suits)
                        {
                            EquipRefusal refusal = CommandEligibility.Equip(board, balance, player, vid, suit);

                            SimulationState after = Clone(board);
                            CommandProcessor.ProcessCommand(after, new GameCommand
                            {
                                type = CommandType.Equip, playerID = player, villagerID = vid, value = (int)suit
                            });

                            bool applied = vid >= 0 && vid < board.villagers.Length &&
                                           after.villagers[vid].suit != board.villagers[vid].suit;

                            Assert.AreEqual(applied, refusal == EquipRefusal.None,
                                $"player {player}, villager {vid}, {suit}, food {board.players[player].food}, " +
                                $"materials {board.players[player].materials}: eligibility said {refusal}");

                            checkedCases++;
                            if (applied) accepted++;
                        }

            Assert.Greater(accepted, 0, "the grid never reached an accepted Equip");
            Assert.Greater(checkedCases - accepted, 0, "the grid never reached a refused Equip");
        }

        [Test]
        public void Equip_NamesTheFirstRefusalTheSimulationChecks()
        {
            GameBalanceData balance = Balance();
            SimulationState s = Board(0, 0, null);

            // Busy comes before suit, node, draft and cost: a moving villager on
            // the enemy's barracks, undrafted, broke, is refused as Busy.
            Assert.AreEqual(EquipRefusal.Busy, CommandEligibility.Equip(s, balance, 0, 5, SuitType.Warrior));
            Assert.AreEqual(EquipRefusal.AlreadySuited, CommandEligibility.Equip(s, balance, 0, 7, SuitType.Guardian));
            Assert.AreEqual(EquipRefusal.NodeNotYours, CommandEligibility.Equip(s, balance, 0, 4, SuitType.Warrior));
            Assert.AreEqual(EquipRefusal.DoesNotFit, CommandEligibility.Equip(s, balance, 0, 1, SuitType.Warrior));
            Assert.AreEqual(EquipRefusal.NotDrafted, CommandEligibility.Equip(s, balance, 0, 0, SuitType.Warrior));
            Assert.AreEqual(EquipRefusal.Dead, CommandEligibility.Equip(s, balance, 0, 9, SuitType.Warrior));
            Assert.AreEqual(EquipRefusal.NotYours, CommandEligibility.Equip(s, balance, 0, 12, SuitType.Warrior));

            // A production suit is not a combat suit, so a Farmer may still be equipped.
            Assert.AreEqual(EquipRefusal.None, CommandEligibility.EquipVillager(s, 0, 8));
        }

        [Test]
        public void EquipSuit_CostRefusalNeedsBothFoodAndMaterials()
        {
            GameBalanceData balance = Balance();
            int[] drafted = { (int)SuitType.Guardian };

            Assert.AreEqual(EquipRefusal.CannotAfford, CommandEligibility.EquipSuit(Board(1, 5, drafted), balance, 0, SuitType.Guardian));
            Assert.AreEqual(EquipRefusal.CannotAfford, CommandEligibility.EquipSuit(Board(5, 0, drafted), balance, 0, SuitType.Guardian));
            Assert.AreEqual(EquipRefusal.None, CommandEligibility.EquipSuit(Board(2, 1, drafted), balance, 0, SuitType.Guardian));
        }

        // ===== RESPAWN =====

        [Test]
        public void Respawn_AcceptedExactlyWhenCommandProcessorRespawns_AtTheShownPrice()
        {
            GameBalanceData balance = Balance();
            int accepted = 0;

            foreach (SimulationState board in Boards())
                for (int player = 0; player < 2; player++)
                    for (int vid = -1; vid <= board.villagers.Length; vid++)
                    {
                        RespawnRefusal refusal = CommandEligibility.Respawn(board, balance, player, vid);
                        int shownCost = CommandEligibility.RespawnCost(board, balance, player);

                        SimulationState after = Clone(board);
                        CommandProcessor.ProcessCommand(after, new GameCommand
                        {
                            type = CommandType.Respawn, playerID = player, villagerID = vid
                        });

                        bool applied = vid >= 0 && vid < board.villagers.Length &&
                                       board.villagers[vid].state == VillagerState.Dead &&
                                       after.villagers[vid].state != VillagerState.Dead;

                        Assert.AreEqual(applied, refusal == RespawnRefusal.None,
                            $"player {player}, villager {vid}, food {board.players[player].food}: eligibility said {refusal}");

                        if (applied)
                        {
                            accepted++;
                            Assert.AreEqual(shownCost, board.players[player].food - after.players[player].food,
                                "the price shown is not the price charged");
                        }
                    }

            Assert.Greater(accepted, 0, "the grid never reached an accepted Respawn");
        }

        [Test]
        public void RespawnCost_CountsOnlySanctuariesThePlayerOwns()
        {
            // Villager 14 works player 0's Sanctuary; villager 15 works one player 1
            // owns. Only 14 discounts: 8 food less 25% is 6. The earlier sheet
            // counted both and showed 4.
            Assert.AreEqual(6, CommandEligibility.RespawnCost(Board(20, 0, null), Balance(), 0));
        }

        [Test]
        public void RespawnCost_NeverFallsBelowOne()
        {
            GameBalanceData balance = Balance();
            balance.respawnCostFood = 1;
            balance.sanctuaryRespawnCostReductionPercent = 100;

            Assert.AreEqual(1, CommandEligibility.RespawnCost(Board(20, 0, null), balance, 0));
        }

        [Test]
        public void RespawnTarget_IsTheLongestWait_SkippingConsumedAndTheOpponents()
        {
            SimulationState s = Board(20, 0, null);

            // 9 waits 30, 10 waits 12, 11 waits 40 but is consumed, 13 waits 45 but is player 1's.
            Assert.AreEqual(9, CommandEligibility.RespawnTarget(s, 0));
            Assert.AreEqual(13, CommandEligibility.RespawnTarget(s, 1));
        }

        [Test]
        public void RespawnTarget_BreaksATieOnTheLowerID_AndIsMinusOneWithNobodyWaiting()
        {
            SimulationState s = Board(20, 0, null);
            s.villagers[10].respawnTicksRemaining = 30;

            Assert.AreEqual(9, CommandEligibility.RespawnTarget(s, 0));

            s.villagers[9].state = VillagerState.Idle;
            s.villagers[10].state = VillagerState.Idle;

            Assert.AreEqual(-1, CommandEligibility.RespawnTarget(s, 0));
        }

        // ===== ALLOCATION =====

        [Test]
        public void Allocation_AcceptedExactlyWhenCommandProcessorSetsIt_WithNoCeiling()
        {
            SimulationState board = Board(0, 0, null);

            for (int player = 0; player < 2; player++)
                for (int node = -1; node <= board.nodes.Length; node++)
                    foreach (int value in new[] { -1, 0, 3, 100000 })
                    {
                        AllocationRefusal refusal = CommandEligibility.Allocation(board, player, node, value);

                        SimulationState after = Clone(board);
                        if (node >= 0 && node < after.nodes.Length) after.nodes[node].materialAllocation = -7;
                        SimulationState before = Clone(after);

                        CommandProcessor.ProcessCommand(after, new GameCommand
                        {
                            type = CommandType.SetAllocation, playerID = player, targetNodeID = node, value = value
                        });

                        bool applied = node >= 0 && node < board.nodes.Length &&
                                       after.nodes[node].materialAllocation != before.nodes[node].materialAllocation;

                        Assert.AreEqual(applied, refusal == AllocationRefusal.None,
                            $"player {player}, node {node}, value {value}: eligibility said {refusal}");
                    }
        }
    }
}
