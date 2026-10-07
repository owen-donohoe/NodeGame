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

        [Test]
        public void LegacyBoardKeys_ReadIdentically()
        {
            string path = Path.Combine(Path.GetTempPath(), "rig-legacy-board-" + System.Guid.NewGuid() + ".asset");
            try
            {
                File.WriteAllText(path, LegacyBoard);
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
            var setup = RigSetupLoader.Load(null, null, "none");
            Assert.AreEqual(4, LinkWeight(setup.board));
        }

        private static int LinkWeight(BoardConfigData board) => board.defaultLinkWeight;
    }
}
