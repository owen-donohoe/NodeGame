using System;
using System;
using System.IO;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.BalanceRig
{
    /// <summary>
    /// The board asset's keys are part of a file Unity wrote and the rig reads
    /// by name. A vocabulary rename of the C# fields must leave that text, and
    /// what the rig makes of it, exactly as it was.
    /// </summary>
    public class RigSetupCompatibilityTests
    {
        // Unity's own layout, with every value distinct from the defaults so a
        // key the loader no longer recognises shows up as a wrong number.
        private const string LegacyBoard =
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            + "  m_Name: LegacyBoard\n"
            + "  data:\n"
            + "    gridCols: 3\n    gridRows: 3\n    defaultEdgeWeight: 7\n"
            + "    startingVillagersPerPlayer: 2\n    startingFood: 11\n    startingMaterials: 12\n    startingMetal: 13\n"
            + "    ownedMultiplier: 51\n    partiallyOwnedMultiplier: 76\n    unownedMultiplier: 101\n"
            + "    enemyPartiallyOwnedMultiplier: 151\n    enemyOwnedMultiplier: 201\n"
            + "    initialPlacements:\n"
            + "    - gridX: 1\n      gridZ: 0\n      districtType: 5\n      ownerID: 0\n      claimBar: 10000\n"
            + "    - gridX: 1\n      gridZ: 2\n      districtType: 5\n      ownerID: 1\n      claimBar: -10000\n"
            + "  nodeScale: 6\n"
            + "  baseDraftNodesP0:\n  - districtType: 1\n  baseDraftNodesP1:\n  - districtType: 2\n";

        // The same board once Unity has re-saved it with the renamed keys.
        private static readonly string RenamedBoard = LegacyBoard
            .Replace("defaultEdgeWeight", "defaultLinkWeight")
            .Replace("baseDraftNodesP0", "baseDraftDistrictsP0")
            .Replace("baseDraftNodesP1", "baseDraftDistrictsP1");

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyBoardKeys_ReadIdentically(bool renamedKeys)
        {
            string path = Path.Combine(Path.GetTempPath(), "rig-legacy-board-" + System.Guid.NewGuid() + ".asset");
            try
            {
                File.WriteAllText(path, renamedKeys ? RenamedBoard : LegacyBoard);
                var setup = new RigSetup();
                RigSetupLoader.LoadBoard(path, setup);

                BoardConfigData b = setup.board;
                Assert.AreEqual((3, 3, 7), (b.gridCols, b.gridRows, LinkWeight(b)));
                Assert.AreEqual((2, 11, 12, 13), (b.startingVillagersPerPlayer, b.startingFood, b.startingMaterials, b.startingMetal));
                Assert.AreEqual((51, 76, 101, 151, 201), (b.ownedMultiplier, b.partiallyOwnedMultiplier, b.unownedMultiplier,
                    b.enemyPartiallyOwnedMultiplier, b.enemyOwnedMultiplier));
                Assert.AreEqual(2, b.initialPlacements.Length);
                Assert.AreEqual((1, 0, DistrictType.Core, 0, 10000),
                    (b.initialPlacements[0].gridX, b.initialPlacements[0].gridZ, b.initialPlacements[0].districtType, b.initialPlacements[0].ownerID, b.initialPlacements[0].claimBar));
                Assert.AreEqual((1, 2, DistrictType.Core, 1, -10000),
                    (b.initialPlacements[1].gridX, b.initialPlacements[1].gridZ, b.initialPlacements[1].districtType, b.initialPlacements[1].ownerID, b.initialPlacements[1].claimBar));
                CollectionAssert.AreEqual(new[] { DistrictType.Farm }, setup.baseDraft[0]);
                CollectionAssert.AreEqual(new[] { DistrictType.Mine }, setup.baseDraft[1]);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ShippedBoardAsset_KeepsItsLegacyKey()
        {
            // The asset is Unity's to rewrite; until it does, its key is the legacy one.
            // The rig no longer plays it (it has no terrain); it can still be read.
            var setup = new RigSetup();
            RigSetupLoader.LoadBoard(Path.Combine(RigSetupLoader.FindRepoRoot(), "Assets/Data/Game/Board/DefaultBoardConfig.asset"), setup);
            Assert.AreEqual(4, LinkWeight(setup.board));
            Assert.IsNull(setup.board.terrain, "A legacy asset has no terrain to read.");
        }

        [Test]
        public void DefaultUsesSameCatalogAsFactory()
        {
            RigSetup setup = RigSetupLoader.Load(null, null, "none");
            BoardConfigData shipped = PremadeMaps.Hourglass01();

            Assert.AreEqual(PremadeMaps.Hourglass01Id, setup.mapId);
            Assert.AreEqual("catalog:hourglass-01", setup.boardSource, "The default board is the catalog's, not a parsed file.");
            Assert.AreEqual(BoardHasher.Hash(shipped), BoardHasher.Hash(setup.board));
            CollectionAssert.AreEqual(shipped.terrain, setup.board.terrain);
            CollectionAssert.AreEqual(shipped.districtSlots, setup.board.districtSlots);
            CollectionAssert.AreEqual(shipped.baseDraftDistrictsP0, setup.baseDraft[0]);
            CollectionAssert.AreEqual(shipped.baseDraftDistrictsP1, setup.baseDraft[1]);

            // The rig builds the very board the live game and the referee build.
            var matchSetup = MatchSetup.ForShippedMap(setup.mapId, setup.balanceHash);
            MatchFactory.Configure(setup.balance, setup.board);
            SimulationState state = MatchFactory.Build(setup.balance, setup.board, new DraftPlacement[0],
                new[] { new PlayerSetup(), new PlayerSetup() });
            Assert.AreEqual(matchSetup.BoardHash, state.boardHash);
            Assert.AreEqual(18, state.nodes.Length);

            // Its symmetry is the map's own: top to bottom, not a half turn.
            Assert.AreEqual((2, 5), setup.mirror(2, 1));
            Assert.AreEqual((0, 6), setup.mirror(0, 0));
            Assert.AreEqual((6, 3), setup.mirror(6, 3));
        }

        [Test]
        public void ALegacyAssetBoard_IsRefusedForAMatch()
        {
            string asset = Path.Combine(RigSetupLoader.FindRepoRoot(), "Assets/Data/Game/Board/DefaultBoardConfig.asset");
            var ex = Assert.Throws<FormatException>(() => RigSetupLoader.Load(null, asset, "none"));
            StringAssert.Contains("terrain", ex.Message);
        }

        [Test]
        public void SwapSeats_OnTheHourglass_MirrorsTerrainConsistently()
        {
            RigSetup setup = RigSetupLoader.Load(null, null, "none");
            PreparedMatch first = MatchRunner.Prepare(setup, 5);
            PreparedMatch paired = MatchRunner.SwapSeats(first);

            // The map is its own mirror image, so mirroring terrain is an identity.
            BoardConfigData a = first.setup.board;
            BoardConfigData b = paired.setup.board;
            CollectionAssert.AreEqual(a.terrain, b.terrain);
            CollectionAssert.AreEqual(a.districtSlots, b.districtSlots);
            CollectionAssert.AreEqual(a.baseDraftDistrictsP0, b.baseDraftDistrictsP1);
            CollectionAssert.AreEqual(a.baseDraftDistrictsP1, b.baseDraftDistrictsP0);
            Assert.IsTrue(MapAuthoringRules.ValidateAuthoredMap(b, out string error), error);

            // The same two Cores, with the players traded.
            foreach (var ip in a.initialPlacements)
            {
                var mirrored = Array.Find(b.initialPlacements, q => q.gridX == ip.gridX && q.gridZ == 6 - ip.gridZ);
                Assert.AreEqual(ip.districtType, mirrored.districtType);
                Assert.AreEqual(1 - ip.ownerID, mirrored.ownerID);
                Assert.AreEqual(-ip.claimBar, mirrored.claimBar);
            }

            // The original is untouched, and both seats' drafts are legal on the shared map.
            Assert.AreEqual(BoardHasher.Hash(PremadeMaps.Hourglass01()), BoardHasher.Hash(setup.board));
            foreach (PreparedMatch match in new[] { first, paired })
            {
                var occupied = new bool[49];
                foreach (DraftPlacement dp in match.draft)
                {
                    Assert.IsTrue(PlacementLegality.CanPlace(match.setup.board, occupied, dp.districtType, dp.gridX, dp.gridZ),
                        match.seat + ": " + dp.districtType + "@" + dp.gridX + "," + dp.gridZ);
                    occupied[dp.gridZ * 7 + dp.gridX] = true;
                }
            }
        }

        [Test]
        public void TheDefaultHourglassPlaysAShortMatchFromALegalDraft()
        {
            RigSetup setup = RigSetupLoader.Load(null, null, "Barracks");
            for (int seed = 1; seed <= 3; seed++)
            {
                MatchResult result = null;
                int s = seed;
                Assert.DoesNotThrow(() => result = MatchRunner.Run(setup, s, 400, 0), "seed " + seed);
                Assert.Greater(result.ticks, 0);
            }
        }

        private static int LinkWeight(BoardConfigData board) => board.defaultLinkWeight;
    }
}
