using System.Linq;
using NodeWar.Simulation;
using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    /// <summary>
    /// What the board shows for each cell, before any Unity object exists: which cells
    /// are node targets, which are legal for the piece in hand, and the non-colour cue
    /// every highlighted cell carries.
    /// </summary>
    public class TerrainPresentationTests
    {
        private static readonly BoardConfigData Board = PremadeMaps.Hourglass01();

        private static CellDescriptor At(CellDescriptor[] cells, int x, int z) => cells[z * 7 + x];

        private static DraftPlacement Pier() =>
            new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 3 };

        [Test]
        public void OceanAndEmptyLakeHaveNoNodeTargets()
        {
            CellDescriptor[] cells = TerrainPresentation.Describe(Board, null, false, DistrictType.None);

            Assert.AreEqual(49, cells.Length);
            Assert.AreEqual(18, cells.Count(c => c.hasNodeTarget), "one target per land node");
            Assert.AreEqual(18, TerrainPresentation.NodeTargetCount(cells));
            foreach (CellDescriptor c in cells)
            {
                if (c.terrain == TerrainType.Ocean) Assert.IsFalse(c.hasNodeTarget, "ocean " + c.x + "," + c.z);
                if (c.terrain == TerrainType.Lake) Assert.IsFalse(c.hasNodeTarget, "lake " + c.x + "," + c.z);
                Assert.AreEqual(c.z * 7 + c.x, c.cell);
            }
            Assert.IsFalse(At(cells, 1, 3).hasNodeTarget, "No pier-slot target until a Pier is placed.");
            Assert.IsTrue(At(cells, 1, 3).isSlot, "but it is a slot");
            Assert.IsFalse(At(cells, 2, 2).isSlot, "open lake is no slot");

            CellDescriptor[] withPier = TerrainPresentation.Describe(Board, new[] { Pier() }, false, DistrictType.None);
            Assert.AreEqual(19, TerrainPresentation.NodeTargetCount(withPier));
            Assert.IsTrue(At(withPier, 1, 3).hasNodeTarget);
            Assert.IsTrue(At(withPier, 1, 3).bridge, "A Pier is drawn as a bridge to its neighbours.");
            Assert.AreEqual(1, withPier.Count(c => c.bridge));
            Assert.IsFalse(At(withPier, 2, 2).hasNodeTarget, "open lake stays untargetable");
            Assert.IsFalse(At(withPier, 0, 0).hasNodeTarget);
        }

        [Test]
        public void LegalCellsCarryAnOutlineAsWellAsATint()
        {
            CellDescriptor[] farm = TerrainPresentation.Describe(Board, null, true, DistrictType.Farm);

            int legal = 0;
            foreach (CellDescriptor c in farm)
            {
                Assert.AreEqual(c.legalForPick, c.tint, "tint follows legality at " + c.x + "," + c.z);
                Assert.AreEqual(c.legalForPick, c.outline, "so does the outline, which does not rely on colour");
                if (c.legalForPick) legal++;
            }
            Assert.AreEqual(16, legal);
            Assert.IsTrue(At(farm, 1, 1).legalForPick);
            Assert.IsFalse(At(farm, 3, 5).legalForPick, "a Core is taken");
            Assert.IsFalse(At(farm, 0, 0).legalForPick, "Ocean is never legal");
            Assert.IsFalse(At(farm, 2, 2).legalForPick, "open lake is never legal");
            Assert.IsFalse(At(farm, 1, 3).legalForPick, "a Farm cannot go on the pier slot");

            CellDescriptor[] pier = TerrainPresentation.Describe(Board, null, true, DistrictType.Pier);
            Assert.AreEqual(1, pier.Count(c => c.legalForPick));
            Assert.IsTrue(At(pier, 1, 3).legalForPick);
            Assert.IsTrue(At(pier, 1, 3).outline);
        }

        [Test]
        public void NothingInHandHighlightsNothing_AndPlacedPiecesAreNotLegal()
        {
            CellDescriptor[] none = TerrainPresentation.Describe(Board, null, false, DistrictType.Farm);
            Assert.AreEqual(0, none.Count(c => c.legalForPick || c.tint || c.outline));

            var placed = new[] { new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 1, gridZ = 1 } };
            CellDescriptor[] cells = TerrainPresentation.Describe(Board, placed, true, DistrictType.Mine);
            Assert.IsTrue(At(cells, 1, 1).occupied);
            Assert.IsFalse(At(cells, 1, 1).legalForPick, "an occupied cell is not offered");
            Assert.AreEqual(15, cells.Count(c => c.legalForPick));
            Assert.IsTrue(At(cells, 3, 5).occupied, "the Core's cell is occupied from the start");
        }

        [Test]
        public void TheHighlightIsTheSameAnswerAsThePureLegalityRule()
        {
            var occupied = new bool[49];
            occupied[2 * 7 + 5] = true;
            var placed = new[] { new DraftPlacement { playerID = 0, districtType = DistrictType.Mine, gridX = 5, gridZ = 2 } };
            foreach (DistrictType pick in new[] { DistrictType.Farm, DistrictType.Pier, DistrictType.Village })
            {
                CellDescriptor[] cells = TerrainPresentation.Describe(Board, placed, true, pick);
                Assert.Greater(cells.Count(c => c.legalForPick), 0, pick + " has somewhere to go");
                var state = new DraftState(Board);
                state.OccupyCell(5, 2);
                foreach (CellDescriptor c in cells)
                    Assert.AreEqual(state.CanPlace(pick, c.x, c.z), c.legalForPick, pick + "@" + c.x + "," + c.z);
            }
        }
    }
}
