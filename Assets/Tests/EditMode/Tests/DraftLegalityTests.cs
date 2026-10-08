using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// The draft's spatial rule has one source, <see cref="PlacementLegality"/>, and
    /// every entry path - local confirm, a received packet, a parked piece the clock
    /// confirms, the timeout's own choice and the highlight - asks it through
    /// <see cref="DraftState"/>. These run the real turn state on the shipped hourglass.
    /// </summary>
    public class DraftLegalityTests
    {
        private static DraftState Draft(params DistrictType[] p0)
        {
            return DraftWith(p0, new[] { DistrictType.Farm });
        }

        private static DraftState DraftWith(DistrictType[] p0, DistrictType[] p1)
        {
            var state = new DraftState(PremadeMaps.Hourglass01());
            state.player0Picks = Picks(p0);
            state.player1Picks = Picks(p1);
            state.phase = DraftPhase.ActiveDraft;
            return state;
        }

        private static DraftPick[] Picks(DistrictType[] types)
        {
            var picks = new DraftPick[types.Length];
            for (int i = 0; i < types.Length; i++) picks[i] = new DraftPick { districtType = types[i] };
            return picks;
        }

        private static int Cell(int x, int z) => z * 7 + x;

        [Test]
        public void EveryEntryPathUsesSameLegalCells()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();

            // Every cell, for a Farm and for a Pier, through every path.
            foreach (DistrictType district in new[] { DistrictType.Farm, DistrictType.Pier })
            {
                DraftState reference = Draft(district);
                bool[] highlight = reference.LegalCells(district);
                Assert.AreEqual(49, highlight.Length);

                for (int z = 0; z < 7; z++)
                    for (int x = 0; x < 7; x++)
                    {
                        bool pure = PlacementLegality.CanPlace(board, reference.occupied, district, x, z);
                        string where = district + "@" + x + "," + z;

                        Assert.AreEqual(pure, reference.CanPlace(district, x, z), "CanPlace " + where);
                        Assert.AreEqual(pure, highlight[Cell(x, z)], "highlight " + where);

                        // local confirm
                        DraftState local = Draft(district);
                        Assert.AreEqual(pure ? 0 : -1, local.AcceptLocal(0, 0, x, z), "local " + where);

                        // a placement packet from the other seat
                        DraftState received = Draft(district);
                        Assert.AreEqual(pure ? 0 : -1, received.AcceptReceived(0, district, x, z), "received " + where);

                        // the piece the player parked, taken when the clock runs out
                        DraftState timeout = Draft(district);
                        bool chosen = timeout.ChooseTimeout(0, 0, x, z, out int slot, out int cx, out int cz);
                        Assert.IsTrue(chosen, "a legal cell always exists for " + where);
                        Assert.IsTrue(timeout.CanPlace(district, cx, cz), "timeout chose a legal cell for " + where);
                        if (pure) Assert.AreEqual((x, z), (cx, cz), "a legal parked piece is kept: " + where);
                        else Assert.AreNotEqual((x, z), (cx, cz), "an illegal parked piece is not forced: " + where);
                    }
            }
        }

        [Test]
        public void EveryEntryPathUsesSameLegalCells_RejectedPacketDoesNotConsumeAPick()
        {
            DraftState state = DraftWith(new[] { DistrictType.Farm, DistrictType.Pier }, new[] { DistrictType.Farm });

            Assert.AreEqual(-1, state.AcceptReceived(0, DistrictType.Farm, 1, 3), "Farm on the pier slot");
            Assert.AreEqual(-1, state.AcceptReceived(0, DistrictType.Farm, 0, 0), "Farm on ocean");
            Assert.AreEqual(-1, state.AcceptReceived(0, DistrictType.Pier, 1, 1), "Pier on land");
            Assert.AreEqual(-1, state.AcceptReceived(0, DistrictType.Village, 1, 1), "a type the player does not hold");
            Assert.AreEqual(-1, state.AcceptLocal(0, 5, 1, 1), "no such pick");
            Assert.AreEqual(-1, state.AcceptLocal(0, -1, 1, 1));

            Assert.IsFalse(state.player0Picks[0].isConsumed);
            Assert.IsFalse(state.player0Picks[1].isConsumed);
            Assert.AreEqual(0, state.confirmedPlacements.Count);
            Assert.IsFalse(state.occupied[Cell(1, 1)], "nothing was occupied by a refusal");

            int farm = state.AcceptReceived(0, DistrictType.Farm, 1, 1);
            Assert.AreEqual(0, farm);
            state.Apply(0, farm, 1, 1, false);
            Assert.IsTrue(state.player0Picks[0].isConsumed);
            Assert.AreEqual(-1, state.AcceptReceived(0, DistrictType.Farm, 1, 2), "the Farm is spent");
            Assert.AreEqual(-1, state.AcceptLocal(0, 0, 1, 2), "a consumed pick is refused locally too");
            Assert.AreEqual(-1, state.AcceptLocal(0, 1, 1, 1), "an occupied cell is refused");
            Assert.AreEqual(1, state.AcceptLocal(0, 1, 1, 3), "and the Pier still has its slot");
        }

        [Test]
        public void NoLegalPierSkipsOnlyThatPick()
        {
            // The only pier slot is taken by the other player's Pier.
            DraftState state = DraftWith(new[] { DistrictType.Pier, DistrictType.Farm },
                                         new[] { DistrictType.Pier });
            state.Apply(1, 0, 1, 3, false);

            Assert.IsFalse(state.HasLegalCell(DistrictType.Pier));
            Assert.IsTrue(state.HasLegalCell(DistrictType.Farm));
            Assert.IsFalse(state.IsPlayable(state.player0Picks[0]), "the Pier has nowhere to go");
            Assert.IsTrue(state.IsPlayable(state.player0Picks[1]), "the Farm is unaffected");
            Assert.IsTrue(state.PlayerHasPlayablePick(0));
            Assert.AreEqual(1, state.GetLowestPlayablePickIndex(0));
            Assert.AreEqual(new[] { 0, 0 }, state.consecutiveTimeouts, "Skipping a pick is not a timeout.");
            Assert.AreEqual(-1, state.AcceptLocal(0, 0, 1, 3), "and the occupied slot stays refused");

            // The Farm is played; the stranded Pier does not keep the draft alive.
            state.Apply(0, 1, 1, 1, false);
            Assert.IsFalse(state.player0Picks[0].isConsumed, "A skipped pick is not refunded or spent.");
            Assert.IsFalse(state.PlayerHasPlayablePick(0));
            Assert.IsFalse(state.PlayerHasPlayablePick(1));
            Assert.IsTrue(state.IsDraftFinished());
            Assert.AreEqual(new[] { 0, 0 }, state.consecutiveTimeouts);
        }

        [Test]
        public void NoLegalPierSkipsOnlyThatPick_BothHoldingOnlyPiersEndsTheDraft()
        {
            DraftState state = DraftWith(new[] { DistrictType.Pier }, new[] { DistrictType.Pier });
            Assert.IsFalse(state.IsDraftFinished());

            state.Apply(0, 0, 1, 3, false);
            Assert.IsTrue(state.PlayerHasPlayablePick(1) == false, "P1's Pier has no slot left");
            Assert.IsTrue(state.IsDraftFinished(), "Neither player has a playable pick.");
            Assert.AreEqual(0, state.consecutiveTimeouts[0] + state.consecutiveTimeouts[1]);
        }

        [Test]
        public void NoLegalPierSkipsOnlyThatPick_TurnPassesPastAPlayerWithNothingPlayable()
        {
            DraftState state = DraftWith(new[] { DistrictType.Pier }, new[] { DistrictType.Farm, DistrictType.Mine });
            state.Apply(0, 0, 1, 3, false); // P0 used the only slot... with their own Pier

            state.currentTurnPlayerID = 0;
            Assert.IsTrue(state.AdvanceToNextValidTurn(), "P1 can still play");
            Assert.AreEqual(1, state.currentTurnPlayerID);

            state.currentTurnPlayerID = 1;
            Assert.IsTrue(state.AdvanceToNextValidTurn());
            Assert.AreEqual(1, state.currentTurnPlayerID, "P1 stays on turn while it has picks and P0 has none.");

            state.Apply(1, 0, 1, 1, false);
            state.Apply(1, 1, 4, 2, false);
            Assert.IsFalse(state.AdvanceToNextValidTurn(), "nobody is left to play");
        }

        [Test]
        public void TimeoutChoosesLowestPlayablePick()
        {
            // Index 0 is a Pier with no slot left; index 1 is a Farm; index 2 another Farm.
            DraftState state = DraftWith(new[] { DistrictType.Pier, DistrictType.Farm, DistrictType.Farm },
                                         new[] { DistrictType.Pier });
            state.Apply(1, 0, 1, 3, false);

            for (int turn = 0; turn < 40; turn++)
            {
                state.turnNumber = turn;
                Assert.IsTrue(state.ChooseTimeout(0, -1, -1, -1, out int slot, out int x, out int z), "turn " + turn);
                Assert.AreEqual(1, slot, "the lowest PLAYABLE pick, not the lowest pick");
                Assert.IsTrue(state.CanPlace(DistrictType.Farm, x, z), "a legal land slot at " + x + "," + z);
                Assert.AreNotEqual(TerrainType.Ocean, state.board.terrain[Cell(x, z)]);
                Assert.AreNotEqual(TerrainType.Lake, state.board.terrain[Cell(x, z)], "never water");
            }

            // Seeded from replicated state only: the same turn picks the same cell.
            state.turnNumber = 9;
            state.ChooseTimeout(0, -1, -1, -1, out _, out int ax, out int az);
            state.ChooseTimeout(0, -1, -1, -1, out _, out int bx, out int bz);
            Assert.AreEqual((ax, az), (bx, bz));

            // A different turn can pick a different cell (the seed matters).
            var cells = new System.Collections.Generic.HashSet<int>();
            for (int turn = 0; turn < 40; turn++)
            {
                state.turnNumber = turn;
                state.ChooseTimeout(0, -1, -1, -1, out _, out int x, out int z);
                cells.Add(Cell(x, z));
            }
            Assert.Greater(cells.Count, 1);
        }

        [Test]
        public void TimeoutChoosesLowestPlayablePick_AParkedPieceIsKeptOnlyWhileItIsStillLegal()
        {
            DraftState state = DraftWith(new[] { DistrictType.Farm, DistrictType.Mine },
                                         new[] { DistrictType.Farm });

            // The Mine parked on a free cell is taken as it stands.
            Assert.IsTrue(state.ChooseTimeout(0, 1, 5, 4, out int slot, out int x, out int z));
            Assert.AreEqual((1, 5, 4), (slot, x, z));

            // The cell was taken meanwhile: fall back to the lowest playable pick.
            state.Apply(1, 0, 5, 4, false);
            Assert.IsTrue(state.ChooseTimeout(0, 1, 5, 4, out slot, out x, out z));
            Assert.AreEqual(0, slot);
            Assert.IsTrue(state.CanPlace(DistrictType.Farm, x, z));

            // The parked pick itself was spent.
            state.Apply(0, 1, 1, 1, false);
            Assert.IsTrue(state.ChooseTimeout(0, 1, 2, 1, out slot, out _, out _));
            Assert.AreEqual(0, slot);

            // Nothing playable: no placement is invented.
            state.Apply(0, 0, 1, 2, false);
            Assert.IsFalse(state.ChooseTimeout(0, -1, -1, -1, out _, out _, out _));
        }

        [Test]
        public void TheLegalCellCounts_MatchTheHourglass()
        {
            DraftState state = Draft(DistrictType.Farm);
            int farm = 0, pier = 0;
            foreach (bool b in state.LegalCells(DistrictType.Farm)) if (b) farm++;
            foreach (bool b in state.LegalCells(DistrictType.Pier)) if (b) pier++;
            Assert.AreEqual(16, farm, "18 land cells minus two Cores");
            Assert.AreEqual(1, pier, "the one pier slot");

            state.Apply(0, 0, 1, 1, false);
            farm = 0;
            foreach (bool b in state.LegalCells(DistrictType.Farm)) if (b) farm++;
            Assert.AreEqual(15, farm);
        }

        [Test]
        public void FindNearestLegalCell_PrefersTheCoreSideAndBreaksTiesByCellIndex()
        {
            DraftState state = Draft(DistrictType.Farm);
            // P0's core is (3,5): the nearest free land slot is next to it.
            Assert.IsTrue(state.FindNearestLegalCell(DistrictType.Farm, 3, 5, out int x, out int z));
            Assert.AreEqual(1, System.Math.Abs(x - 3) + System.Math.Abs(z - 5));
            Assert.AreEqual((2, 5), (x, z), "of the equidistant (2,5) and (4,5) the lower cell index wins");
            Assert.IsTrue(state.FindNearestLegalCell(DistrictType.Pier, 3, 5, out x, out z));
            Assert.AreEqual((1, 3), (x, z));
        }

        [Test]
        public void TestingPlacements_AreLegalDeterministicAndBuildTheHourglass()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            DraftPlacement[] a = DraftPlanner.TestingPlacements(board);
            DraftPlacement[] b = DraftPlanner.TestingPlacements(board);

            Assert.AreEqual(6, a.Length, "Farm, Mine and Village for each player");
            Assert.AreEqual(a.Length, b.Length);
            var occupied = new bool[49];
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual((a[i].playerID, a[i].districtType, a[i].gridX, a[i].gridZ),
                    (b[i].playerID, b[i].districtType, b[i].gridX, b[i].gridZ));
                Assert.IsTrue(PlacementLegality.CanPlace(board, occupied, a[i].districtType, a[i].gridX, a[i].gridZ), "placement " + i);
                occupied[a[i].gridZ * 7 + a[i].gridX] = true;
                Assert.AreEqual(i % 2, a[i].playerID, "players alternate, P0 first");
            }

            // Each side's pieces sit on that side of the lake.
            foreach (DraftPlacement p in a)
                if (p.playerID == 0) Assert.GreaterOrEqual(p.gridZ, 3);
                else Assert.LessOrEqual(p.gridZ, 3);

            GameBalanceData balance = GameBalanceData.Default();
            MatchFactory.Configure(balance, board);
            SimulationState state = MatchFactory.Build(balance, board, a,
                new[] { new PlayerSetup(), new PlayerSetup() });
            Assert.AreEqual(18, state.nodes.Length);
        }

        [Test]
        public void AppliedPlacements_ReplayThroughTheFactory()
        {
            // What the draft decides is exactly what MatchFactory accepts.
            BoardConfigData board = PremadeMaps.Hourglass01();
            DraftState state = DraftWith(new[] { DistrictType.Farm, DistrictType.Pier },
                                         new[] { DistrictType.Village });
            state.Apply(0, 1, 1, 3, false);
            state.Apply(1, 0, 4, 2, false);
            state.Apply(0, 0, 4, 4, true);

            Assert.AreEqual(3, state.confirmedPlacements.Count);
            Assert.AreEqual(1, state.consecutiveTimeouts[0]);
            GameBalanceData balance = GameBalanceData.Default();
            MatchFactory.Configure(balance, board);
            SimulationState built = null;
            Assert.DoesNotThrow(() => built = MatchFactory.Build(balance, board,
                state.confirmedPlacements.ToArray(), new[] { new PlayerSetup(), new PlayerSetup() }));
            Assert.AreEqual(19, built.nodes.Length, "18 land nodes and the Pier");
        }
    }
}
