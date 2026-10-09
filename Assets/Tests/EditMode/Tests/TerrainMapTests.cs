using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// The terrain-aware starting board and the authored hourglass-01 map:
    /// sparse node IDs, links that never jump water, placement legality, the
    /// map validators and the board fingerprint. Tests run sequentially because
    /// MatchFactory.Configure sets process globals.
    /// </summary>
    public class TerrainMapTests
    {
        private delegate void MutateBoard(ref BoardConfigData board);

        private static GameBalanceData Balance()
        {
            return GameBalanceData.Default();
        }

        private static PlayerSetup[] Players()
        {
            return new[]
            {
                new PlayerSetup { suits = new int[0], districts = new int[0] },
                new PlayerSetup { suits = new int[0], districts = new int[0] }
            };
        }

        private static DraftPlacement PierAt13()
        {
            return new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 3 };
        }

        private static SimulationState Build(BoardConfigData board, params DraftPlacement[] draft)
        {
            GameBalanceData balance = Balance();
            MatchFactory.Configure(balance, board);
            return MatchFactory.Build(balance, board, draft, Players());
        }

        private static int Cell(BoardConfigData board, int x, int z)
        {
            return z * board.gridCols + x;
        }

        // --- the sparse board ---

        [Test]
        public void Hourglass_NoPierHas18Nodes_WithPierHas19()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            Assert.AreEqual(49, board.gridCols * board.gridRows);

            SimulationState noPier = Build(board);
            int[] noPierMap = MatchFactory.CellToNode(board, null);
            Assert.AreEqual(18, noPier.nodes.Length);
            Assert.AreEqual(49, noPierMap.Length);
            for (int i = 0; i < noPier.nodes.Length; i++) Assert.AreEqual(i, noPier.nodes[i].nodeID);
            Assert.AreEqual(31, CountAbsent(noPierMap), "18 of 49 cells carry a node.");
            Assert.AreEqual(-1, noPierMap[Cell(board, 1, 3)], "The empty Pier slot is not a node.");
            Assert.AreEqual(-1, noPierMap[Cell(board, 0, 0)], "Ocean is never a node.");
            Assert.AreEqual(-1, noPierMap[Cell(board, 2, 2)], "Open lake is never a node.");

            SimulationState withPier = Build(board, PierAt13());
            int[] pierMap = MatchFactory.CellToNode(board, new[] { PierAt13() });
            Assert.AreEqual(19, withPier.nodes.Length);
            for (int i = 0; i < withPier.nodes.Length; i++) Assert.AreEqual(i, withPier.nodes[i].nodeID);
            Assert.AreEqual(30, CountAbsent(pierMap));

            int pier = pierMap[Cell(board, 1, 3)];
            Assert.GreaterOrEqual(pier, 0);
            Assert.AreEqual(DistrictType.Pier, withPier.nodes[pier].districtType);
            Assert.AreEqual(TerrainType.Lake, withPier.nodes[pier].terrain);
            Assert.AreEqual(-1, withPier.nodes[pier].ownerID, "A drafted Pier starts unowned.");
            for (int i = 0; i < withPier.nodes.Length; i++)
                if (i != pier) Assert.AreEqual(TerrainType.Land, withPier.nodes[i].terrain);
            Assert.AreEqual(1, withPier.nodes[pier].gridX);
            Assert.AreEqual(3, withPier.nodes[pier].gridZ);
        }

        private static int CountAbsent(int[] map)
        {
            int n = 0;
            for (int i = 0; i < map.Length; i++) if (map[i] < 0) n++;
            return n;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SparseIds_AreRowMajorAndLinksHaveNoWaterJump(bool withPier)
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            DraftPlacement[] draft = withPier ? new[] { PierAt13() } : new DraftPlacement[0];
            SimulationState state = Build(board, draft);
            int[] cellToNode = MatchFactory.CellToNode(board, draft);

            int previousCell = -1;
            for (int i = 0; i < state.nodes.Length; i++)
            {
                NodeData node = state.nodes[i];
                int cell = Cell(board, node.gridX, node.gridZ);
                Assert.Greater(cell, previousCell, "Node IDs follow ascending cell index.");
                previousCell = cell;
                Assert.AreEqual(i, cellToNode[cell]);

                // Left, right, down, up; absent neighbours are skipped, never jumped.
                var expected = new System.Collections.Generic.List<int>();
                foreach (int neighbour in new[]
                {
                    node.gridX > 0 ? cell - 1 : -1,
                    node.gridX < board.gridCols - 1 ? cell + 1 : -1,
                    node.gridZ > 0 ? cell - board.gridCols : -1,
                    node.gridZ < board.gridRows - 1 ? cell + board.gridCols : -1
                })
                    if (neighbour >= 0 && cellToNode[neighbour] >= 0) expected.Add(cellToNode[neighbour]);

                Assert.AreEqual(expected.Count, node.links.Length, "node " + i);
                for (int l = 0; l < node.links.Length; l++)
                {
                    Assert.AreEqual(expected[l], node.links[l].toNodeID, "node " + i + " link " + l);
                    Assert.AreEqual(4, node.links[l].travelWeight);

                    NodeData to = state.nodes[node.links[l].toNodeID];
                    Assert.AreEqual(1, Math.Abs(to.gridX - node.gridX) + Math.Abs(to.gridZ - node.gridZ),
                        "Links join orthogonal neighbours only.");
                    bool reverse = false;
                    foreach (Link back in to.links)
                        if (back.toNodeID == i) { reverse = true; Assert.AreEqual(4, back.travelWeight); }
                    Assert.IsTrue(reverse, "Every link has a reverse link.");
                }
            }

            // The west bank cannot cross the open lake: (1,2) and (1,4) never touch.
            NodeData south = state.nodes[cellToNode[Cell(board, 1, 2)]];
            NodeData north = state.nodes[cellToNode[Cell(board, 1, 4)]];
            Assert.AreEqual(withPier ? 2 : 1, south.links.Length);
            Assert.AreEqual(withPier ? 2 : 1, north.links.Length);
            foreach (Link link in south.links) Assert.AreNotEqual(north.nodeID, link.toNodeID);
        }

        // --- the authored map ---

        [Test]
        public void Hourglass_IsMirrorSymmetric()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            Assert.AreEqual("hourglass-01", PremadeMaps.Hourglass01Id);
            Assert.AreEqual(7, board.gridCols);
            Assert.AreEqual(7, board.gridRows);

            for (int z = 0; z < 7; z++)
                for (int x = 0; x < 7; x++)
                {
                    Assert.AreEqual(board.terrain[Cell(board, x, z)], board.terrain[Cell(board, x, 6 - z)], "terrain " + x + "," + z);
                    Assert.AreEqual(board.districtSlots[Cell(board, x, z)], board.districtSlots[Cell(board, x, 6 - z)], "slot " + x + "," + z);
                }

            Assert.AreEqual(2, board.initialPlacements.Length);
            BoardConfigData.InitialDistrictPlacement p0 = board.initialPlacements[0];
            BoardConfigData.InitialDistrictPlacement p1 = board.initialPlacements[1];
            Assert.AreEqual((3, 5, DistrictType.Core, 0, 10000), (p0.gridX, p0.gridZ, p0.districtType, p0.ownerID, p0.claimBar));
            Assert.AreEqual((3, 1, DistrictType.Core, 1, -10000), (p1.gridX, p1.gridZ, p1.districtType, p1.ownerID, p1.claimBar));
            CollectionAssert.AreEqual(board.baseDraftDistrictsP0, board.baseDraftDistrictsP1);
            CollectionAssert.AreEqual(new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village }, board.baseDraftDistrictsP0);
            Assert.AreEqual(3, board.startingVillagersPerPlayer);
            Assert.AreEqual(4, board.defaultLinkWeight);

            Assert.IsTrue(MapAuthoringRules.ValidateAuthoredMap(board, out string error), error);

            BoardConfigData lake = PremadeMaps.Hourglass01();
            lake.terrain[Cell(lake, 2, 2)] = TerrainType.Land;
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(lake, out error));
            StringAssert.Contains("(2,2)", error);

            BoardConfigData owner = PremadeMaps.Hourglass01();
            owner.initialPlacements[0].ownerID = 1;
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(owner, out error));

            BoardConfigData pick = PremadeMaps.Hourglass01();
            pick.baseDraftDistrictsP1[0] = DistrictType.Mine;
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(pick, out error));
        }

        [Test]
        public void Hourglass_HasLandRouteWithoutPier()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            Assert.IsTrue(MapAuthoringRules.HasLandRoute(board));

            BoardConfigData cut = PremadeMaps.Hourglass01();
            cut.terrain[Cell(cut, 4, 3)] = TerrainType.Lake;
            cut.terrain[Cell(cut, 5, 3)] = TerrainType.Lake;
            Assert.IsFalse(MapAuthoringRules.HasLandRoute(cut),
                "The pier slot at (1,3) is lake and must not rescue the land route.");
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(cut, out string error));
            StringAssert.Contains("route", error);
        }

        [Test]
        public void AuthoredMap_NodeCountMustStayWithin14To20()
        {
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(BoardFixtures.LandGrid3x3(), out string error),
                "9 nodes is below the authored minimum.");
            StringAssert.Contains("nodes", error);

            BoardConfigData wide = BoardFixtures.LandGrid(5, 5);
            Assert.IsFalse(MapAuthoringRules.ValidateAuthoredMap(wide, out error), "25 land nodes is above 20.");
            StringAssert.Contains("nodes", error);
        }

        [Test]
        public void CoreLookup_MissingDuplicateOrWaterCoreIsRefused()
        {
            var malformed = new System.Collections.Generic.Dictionary<string, BoardConfigData>();

            BoardConfigData missing = BoardFixtures.LandGrid3x3();
            missing.initialPlacements = new[] { missing.initialPlacements[0] };
            malformed["missing"] = missing;

            BoardConfigData none = BoardFixtures.LandGrid3x3();
            none.initialPlacements = new BoardConfigData.InitialDistrictPlacement[0];
            malformed["none"] = none;

            BoardConfigData third = BoardFixtures.LandGrid3x3();
            var three = new BoardConfigData.InitialDistrictPlacement[3];
            Array.Copy(third.initialPlacements, three, 2);
            three[2] = new BoardConfigData.InitialDistrictPlacement { gridX = 0, gridZ = 1, districtType = DistrictType.Core, ownerID = 0 };
            third.initialPlacements = three;
            malformed["three cores"] = third;

            BoardConfigData sameCell = BoardFixtures.LandGrid3x3();
            sameCell.initialPlacements[1].gridX = sameCell.initialPlacements[0].gridX;
            sameCell.initialPlacements[1].gridZ = sameCell.initialPlacements[0].gridZ;
            malformed["same cell"] = sameCell;

            BoardConfigData sameRow = BoardFixtures.LandGrid3x3();
            sameRow.initialPlacements[1].gridZ = sameRow.initialPlacements[0].gridZ;
            sameRow.initialPlacements[1].gridX = 0;
            malformed["equal Z"] = sameRow;

            BoardConfigData lakeCore = BoardFixtures.LandGrid3x3();
            lakeCore.terrain[Cell(lakeCore, 1, 2)] = TerrainType.Lake;
            malformed["lake core"] = lakeCore;

            BoardConfigData oceanCore = BoardFixtures.LandGrid3x3();
            oceanCore.terrain[Cell(oceanCore, 1, 0)] = TerrainType.Ocean;
            malformed["ocean core"] = oceanCore;

            BoardConfigData outside = BoardFixtures.LandGrid3x3();
            outside.initialPlacements[0].gridX = 9;
            malformed["out of range"] = outside;

            foreach (var entry in malformed)
            {
                BoardConfigData board = entry.Value;
                Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), board, null, Players()), entry.Key);

                var state = new SimulationState();
                Assert.Throws<ArgumentException>(() => MatchFactory.Fill(state, Balance(), board, null, Players()), entry.Key);
                Assert.IsNull(state.nodes, entry.Key + ": nothing is allocated before the board is refused.");
                Assert.IsNull(state.players, entry.Key);
                Assert.IsNull(state.villagers, entry.Key);
            }
        }

        [Test]
        public void CoreLookup_ARefusedBoardNeverFallsBackToTheOldDefaultCores()
        {
            var state = new SimulationState
            {
                nodes = new[] { new NodeData { nodeID = 0, districtType = DistrictType.None } }
            };
            Assert.Throws<InvalidOperationException>(() => MatchFactory.FindCoreNodeID(state, 0));
            Assert.Throws<InvalidOperationException>(() => MatchFactory.FindCoreNodeID(state, 1));
        }

        [Test]
        public void BoardsWithoutTerrain_AreRefusedForANewMatch()
        {
            BoardConfigData legacy = BoardFixtures.LandGrid3x3();
            legacy.terrain = null;
            legacy.districtSlots = null;
            Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), legacy, null, Players()));

            BoardConfigData shortTerrain = BoardFixtures.LandGrid3x3();
            shortTerrain.terrain = new TerrainType[8];
            Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), shortTerrain, null, Players()));

            BoardConfigData shortSlots = BoardFixtures.LandGrid3x3();
            shortSlots.districtSlots = new bool[10];
            Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), shortSlots, null, Players()));

            BoardConfigData unknownTerrain = BoardFixtures.LandGrid3x3();
            unknownTerrain.terrain[0] = (TerrainType)3;
            Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), unknownTerrain, null, Players()));
        }

        [Test]
        public void IllegalDraftPlacements_AreRefusedByTheFactory()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            var cases = new[]
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 3 },  // lake slot
                new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 1 },  // pier on land
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 0, gridZ = 0 },  // ocean
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 3, gridZ = 5 },  // core
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 7, gridZ = 0 }   // outside
            };
            foreach (DraftPlacement bad in cases)
            {
                DraftPlacement placement = bad;
                Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), board, new[] { placement }, Players()),
                    placement.districtType + "@" + placement.gridX + "," + placement.gridZ);
            }

            var twice = new[]
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 1 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Mine, gridX = 1, gridZ = 1 }
            };
            Assert.Throws<ArgumentException>(() => MatchFactory.Build(Balance(), board, twice, Players()), "same cell twice");
        }

        // --- placement legality ---

        [Test]
        public void PlacementLegality_TerrainSlotOccupancyMatrix()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();

            Assert.IsTrue(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 1, 1), "Farm on free land");
            Assert.IsTrue(PlacementLegality.CanPlace(board, null, DistrictType.Barracks, 5, 4), "Barracks on free land");
            Assert.IsTrue(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 1, 3), "Pier on the pier slot");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 1, 3), "Farm on the pier slot");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 1, 1), "Pier on ordinary land");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 2, 2), "Pier on lake without a slot");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 2, 2), "Farm on open lake");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 0, 0), "Farm on ocean");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 0, 0), "Pier on ocean");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 3, 5), "any district on P0's core");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Village, 3, 1), "any district on P1's core");

            var occupied = new bool[board.gridCols * board.gridRows];
            Assert.IsTrue(PlacementLegality.CanPlace(board, occupied, DistrictType.Farm, 1, 1));
            occupied[Cell(board, 1, 1)] = true;
            Assert.IsFalse(PlacementLegality.CanPlace(board, occupied, DistrictType.Farm, 1, 1), "occupied land");
            occupied[Cell(board, 1, 3)] = true;
            Assert.IsFalse(PlacementLegality.CanPlace(board, occupied, DistrictType.Pier, 1, 3), "occupied pier slot");

            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, -1, 1));
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 7, 1));
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 1, -1));
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Farm, 1, 7));
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 7, 3));

            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.None, 1, 1), "None is not a district to draft");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Core, 1, 1), "Core is not a district to draft");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, (DistrictType)99, 1, 1), "unknown type");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, (DistrictType)(-1), 1, 1), "negative type");
            Assert.IsFalse(PlacementLegality.CanPlace(board, null, DistrictType.Pier, 1, 1));

            // A land cell the board does not mark as a slot is not draftable.
            BoardConfigData closed = PremadeMaps.Hourglass01();
            closed.districtSlots[Cell(closed, 1, 1)] = false;
            Assert.IsFalse(PlacementLegality.CanPlace(closed, null, DistrictType.Farm, 1, 1));

            // A board without terrain or slots offers nowhere to build.
            BoardConfigData bare = PremadeMaps.Hourglass01();
            bare.terrain = null;
            Assert.IsFalse(PlacementLegality.CanPlace(bare, null, DistrictType.Farm, 1, 1));
            BoardConfigData noSlots = PremadeMaps.Hourglass01();
            noSlots.districtSlots = null;
            Assert.IsFalse(PlacementLegality.CanPlace(noSlots, null, DistrictType.Farm, 1, 1));
        }

        private static SimulationState EnemyPierTransit()
        {
            GameBalanceData balance = Balance();
            MatchFactory.Configure(balance, PremadeMaps.Hourglass01());
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = state.nodes[1].baseDistrictType = DistrictType.Pier;
            state.nodes[1].ownerID = 1; state.nodes[1].claimBar = -balance.claimThreshold;
            state.villagers[1].state = VillagerState.Dead; state.villagers[1].hp = 0;
            state.villagers[1].respawnTicksRemaining = 100000;
            CommandProcessor.ProcessCommand(state, new GameCommand
            { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 2 });
            for (int i = 0; i < 2 * balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.villagers[0].currentNodeID);
            Assert.AreEqual(VillagerState.Claiming, state.villagers[0].state);
            Assert.AreEqual(2, state.villagers[0].targetNodeID);
            Assert.Greater(state.nodes[1].claimBar, -balance.claimThreshold);
            Assert.AreEqual(1, state.nodes[1].ownerID);
            return state;
        }
        [Test] public void Pier_DStageBlocksEnemyTransit() => EnemyPierTransit();
        [Test] public void Pier_DStageBlocksEnemyTransit_Determinism() =>
            Assert.AreEqual(SimulationStateHasher.ComputeHash(EnemyPierTransit()), SimulationStateHasher.ComputeHash(EnemyPierTransit()));

        // --- the board fingerprint ---

        [Test]
        public void BoardHash_EachTerrainSlotAndSetupMutationChangesHash()
        {
            BoardConfigData baseline = PremadeMaps.Hourglass01();
            int expected = BoardHasher.Hash(baseline);
            Assert.AreEqual(expected, BoardHasher.Hash(PremadeMaps.Hourglass01()), "Equal boards hash equally.");
            Assert.AreEqual(expected, BoardHasher.Hash(CopyOf(baseline)), "A copy hashes like its source.");

            void Changes(string what, MutateBoard mutate)
            {
                BoardConfigData board = CopyOf(baseline);
                mutate(ref board);
                Assert.AreNotEqual(expected, BoardHasher.Hash(board), what);
                Assert.AreEqual(expected, BoardHasher.Hash(baseline), what + ": the source is untouched.");
            }

            for (int i = 0; i < baseline.terrain.Length; i++)
            {
                int cell = i;
                foreach (TerrainType other in new[] { TerrainType.Land, TerrainType.Lake, TerrainType.Ocean })
                    if (other != baseline.terrain[cell])
                        Changes("terrain " + cell + "->" + other, (ref BoardConfigData b) => b.terrain[cell] = other);
                Changes("slot " + cell, (ref BoardConfigData b) => b.districtSlots[cell] = !b.districtSlots[cell]);
            }

            Changes("gridCols", (ref BoardConfigData b) => b.gridCols++);
            Changes("gridRows", (ref BoardConfigData b) => b.gridRows++);
            Changes("defaultLinkWeight", (ref BoardConfigData b) => b.defaultLinkWeight++);
            Changes("startingVillagersPerPlayer", (ref BoardConfigData b) => b.startingVillagersPerPlayer++);
            Changes("startingFood", (ref BoardConfigData b) => b.startingFood++);
            Changes("startingMaterials", (ref BoardConfigData b) => b.startingMaterials++);
            Changes("startingMetal", (ref BoardConfigData b) => b.startingMetal++);
            Changes("ownedMultiplier", (ref BoardConfigData b) => b.ownedMultiplier++);
            Changes("partiallyOwnedMultiplier", (ref BoardConfigData b) => b.partiallyOwnedMultiplier++);
            Changes("unownedMultiplier", (ref BoardConfigData b) => b.unownedMultiplier++);
            Changes("enemyPartiallyOwnedMultiplier", (ref BoardConfigData b) => b.enemyPartiallyOwnedMultiplier++);
            Changes("enemyOwnedMultiplier", (ref BoardConfigData b) => b.enemyOwnedMultiplier++);

            for (int p = 0; p < baseline.initialPlacements.Length; p++)
            {
                int index = p;
                Changes("placement " + index + " x", (ref BoardConfigData b) => b.initialPlacements[index].gridX++);
                Changes("placement " + index + " z", (ref BoardConfigData b) => b.initialPlacements[index].gridZ++);
                Changes("placement " + index + " district", (ref BoardConfigData b) => b.initialPlacements[index].districtType = DistrictType.Forge);
                Changes("placement " + index + " owner", (ref BoardConfigData b) => b.initialPlacements[index].ownerID = 1 - b.initialPlacements[index].ownerID);
                Changes("placement " + index + " claimBar", (ref BoardConfigData b) => b.initialPlacements[index].claimBar++);
            }
            Changes("placement count", (ref BoardConfigData b) => b.initialPlacements = new[] { b.initialPlacements[0] });
            Changes("placement order", (ref BoardConfigData b) =>
            {
                var swapped = new[] { b.initialPlacements[1], b.initialPlacements[0] };
                b.initialPlacements = swapped;
            });

            for (int i = 0; i < baseline.baseDraftDistrictsP0.Length; i++)
            {
                int index = i;
                Changes("P0 pool " + index, (ref BoardConfigData b) => b.baseDraftDistrictsP0[index] = DistrictType.Barracks);
                Changes("P1 pool " + index, (ref BoardConfigData b) => b.baseDraftDistrictsP1[index] = DistrictType.Barracks);
            }
            Changes("P0 pool length", (ref BoardConfigData b) => b.baseDraftDistrictsP0 = new[] { DistrictType.Farm, DistrictType.Mine });
            Changes("P1 pool length", (ref BoardConfigData b) => b.baseDraftDistrictsP1 = new[] { DistrictType.Farm, DistrictType.Mine });
            Changes("P0 pool order", (ref BoardConfigData b) => b.baseDraftDistrictsP0 = new[] { DistrictType.Village, DistrictType.Mine, DistrictType.Farm });
            Changes("pool swap between players", (ref BoardConfigData b) =>
            {
                b.baseDraftDistrictsP0 = new[] { DistrictType.Farm, DistrictType.Mine };
                b.baseDraftDistrictsP1 = new[] { DistrictType.Village };
            });
            Changes("terrain missing", (ref BoardConfigData b) => b.terrain = null);
            Changes("slots missing", (ref BoardConfigData b) => b.districtSlots = null);
        }

        [Test]
        public void BoardHash_IgnoresTheDraft()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            GameBalanceData balance = Balance();
            MatchFactory.Configure(balance, board);
            SimulationState empty = MatchFactory.Build(balance, board, null, Players());
            SimulationState piered = MatchFactory.Build(balance, board, new[] { PierAt13() }, Players());
            Assert.AreEqual(empty.boardHash, piered.boardHash, "The draft is agreed later and is not board identity.");
            Assert.AreEqual(BoardHasher.Hash(board), empty.boardHash);
            Assert.AreNotEqual(0, empty.boardHash);
        }

        private static BoardConfigData CopyOf(BoardConfigData board)
        {
            BoardConfigData copy = board;
            copy.initialPlacements = (BoardConfigData.InitialDistrictPlacement[])board.initialPlacements.Clone();
            copy.terrain = (TerrainType[])board.terrain.Clone();
            copy.districtSlots = (bool[])board.districtSlots.Clone();
            copy.baseDraftDistrictsP0 = (DistrictType[])board.baseDraftDistrictsP0.Clone();
            copy.baseDraftDistrictsP1 = (DistrictType[])board.baseDraftDistrictsP1.Clone();
            return copy;
        }

        // --- hasher and copy guards for the new state ---

        [Test]
        public void Terrain_AloneChangesTheStateHash_AndSurvivesACopy()
        {
            SimulationState state = Build(BoardFixtures.LandGrid3x3());
            int baseline = SimulationStateHasher.ComputeHash(state);

            var copy = new SimulationState();
            copy.CopyFrom(state);
            Assert.AreEqual(baseline, SimulationStateHasher.ComputeHash(copy));

            copy.nodes[4].terrain = TerrainType.Lake;
            Assert.AreNotEqual(baseline, SimulationStateHasher.ComputeHash(copy));
            Assert.AreEqual(TerrainType.Land, state.nodes[4].terrain, "Mutating the copy leaves the source alone.");

            var again = new SimulationState();
            again.CopyFrom(copy);
            Assert.AreEqual(TerrainType.Lake, again.nodes[4].terrain);
        }

        [Test]
        public void BoardHash_AloneChangesTheStateHash_AndSurvivesACopy()
        {
            SimulationState state = Build(BoardFixtures.LandGrid3x3());
            int baseline = SimulationStateHasher.ComputeHash(state);

            var copy = new SimulationState();
            copy.CopyFrom(state);
            Assert.AreEqual(state.boardHash, copy.boardHash);

            copy.boardHash++;
            Assert.AreNotEqual(baseline, SimulationStateHasher.ComputeHash(copy));
            Assert.AreEqual(baseline, SimulationStateHasher.ComputeHash(state));
            Assert.AreEqual(0, new SimulationState().boardHash, "A bare state has no board identity yet.");
        }

        [Test]
        public void FactoryBuildFill_AreIdentical()
        {
            foreach (bool withPier in new[] { false, true })
            {
                BoardConfigData board = PremadeMaps.Hourglass01();
                DraftPlacement[] draft = withPier
                    ? new[] { PierAt13(), new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 4, gridZ = 2 } }
                    : new DraftPlacement[0];
                GameBalanceData balance = Balance();
                MatchFactory.Configure(balance, board);

                SimulationState built = MatchFactory.Build(balance, board, draft, Players());
                var filled = new SimulationState();
                MatchFactory.Fill(filled, balance, board, draft, Players());

                Assert.AreEqual(SimulationStateHasher.ComputeHash(built), SimulationStateHasher.ComputeHash(filled));
                Assert.AreEqual(built.boardHash, filled.boardHash);
                Assert.AreEqual(built.nodes.Length, filled.nodes.Length);
                for (int i = 0; i < built.nodes.Length; i++) Assert.AreEqual(built.nodes[i].terrain, filled.nodes[i].terrain);

                SimulationState second = MatchFactory.Build(balance, board, draft, Players());
                Assert.AreEqual(SimulationStateHasher.ComputeHash(built), SimulationStateHasher.ComputeHash(second),
                    "Two independent builds hash alike.");
            }
        }

        [Test]
        public void HourglassStart_PutsEachPlayerOnTheirOwnCore()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            GameBalanceData balance = Balance();
            SimulationState state = Build(board);

            int p0 = MatchFactory.CellToNode(board, null)[Cell(board, 3, 5)];
            int p1 = MatchFactory.CellToNode(board, null)[Cell(board, 3, 1)];
            Assert.AreEqual(p0, state.players[0].coreNodeID, "P0 holds the high-Z core.");
            Assert.AreEqual(p1, state.players[1].coreNodeID, "P1 holds the low-Z core.");
            Assert.AreEqual(0, state.nodes[p0].ownerID);
            Assert.AreEqual(balance.claimThreshold, state.nodes[p0].claimBar);
            Assert.AreEqual(1, state.nodes[p1].ownerID);
            Assert.AreEqual(-balance.claimThreshold, state.nodes[p1].claimBar);
            Assert.AreEqual(6, state.villagers.Length);
            for (int v = 0; v < 6; v++)
                Assert.AreEqual(state.players[v < 3 ? 0 : 1].coreNodeID, state.villagers[v].currentNodeID);
        }
    }
}
