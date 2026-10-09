using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.View.Tests
{
    public class StructurePresentationTests
    {

        [Test] public void StorehouseFallbackHasDistinctLabelAndIncome()
        {
            var d = DistrictFallback.Describe(DistrictType.Storehouse);
            Assert.That((d.Monogram, d.Name), Is.EqualTo(("S", "Storehouse")));
            var n = new NodeData { bankFood = 2, bankMaterials = 2, bankMetal = 1 };
            Assert.That(StructurePresentation.PipCount(n), Is.EqualTo(5));
            Assert.That(new[] { StructurePresentation.PipResource(n,0), StructurePresentation.PipResource(n,1),
                StructurePresentation.PipResource(n,2), StructurePresentation.PipResource(n,3), StructurePresentation.PipResource(n,4) },
                Is.EqualTo(new[] { 0, 0, 1, 1, 2 }));
            Assert.That(StructurePresentation.WorkerPresentation(DistrictType.Storehouse), Is.False);
            var b=GameBalanceData.Default(); n.districtType=DistrictType.Storehouse; n.ownerID=0; n.claimBar=b.claimThreshold-1;
            n.collectProgress=10; n.collectRequested=true;
            var s=new SimulationState { nodes=new[] { n },players=new PlayerData[2],villagers=new VillagerData[0] };
            int hash=SimulationStateHasher.ComputeHash(s);
            var display=new NodeWar.UI.BankActionModel().Describe(s,b,0,0);
            StringAssert.Contains("Bank 5 / 5",display.Information);
            StringAssert.Contains("Collection 10 / 16: Locked",display.Information);
            Assert.That(display.Workers,Is.False);
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash));
        }

        [Test] public void LayeringAndSeatOffsetsStayClear()
        {
            Assert.That(StructurePresentation.BackOrder, Is.EqualTo(402));
            Assert.That(StructurePresentation.FillOrder, Is.EqualTo(403));
            foreach (int order in new[] { StructurePresentation.BackOrder, StructurePresentation.FillOrder })
            {
                Assert.That(order, Is.GreaterThan(TerrainPresentation.OutlineSortingOrder));
                Assert.That(order, Is.GreaterThan(TerrainPresentation.GroundSortingOrder));
                Assert.That(order, Is.GreaterThan(TerrainPresentation.TintSortingOrder));
                Assert.That(order, Is.Not.EqualTo(400)); Assert.That(order, Is.Not.EqualTo(401));
            }
            StructurePresentation.Offset(0, out float x0, out float z0);
            StructurePresentation.Offset(2, out float x2, out float z2);
            Assert.That((x2,z2), Is.EqualTo((-x0,-z0)));
        }
        [Test] public void BankSheetsLeaveRoomForReadoutsAndFixedControls()
        {
            foreach (var type in new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Storehouse, DistrictType.Forge })
            {
                int actions = type == DistrictType.Forge ? 170 : 110;
                int height = StructurePresentation.BankSheetHeight(type, false);
                Assert.That(height - 80 - actions, Is.GreaterThanOrEqualTo(160), "Bank readout and production body must remain visible");
                Assert.That(StructurePresentation.BankSheetHeight(type, true) - height, Is.EqualTo(44));
            }
            Assert.That(StructurePresentation.BankSheetHeight(DistrictType.Core, false), Is.Zero);
            Assert.That(StructurePresentation.ForgeContentHeight * StructurePresentation.BankPanelHeight, Is.GreaterThanOrEqualTo(136));
            Assert.That(StructurePresentation.ForgeContentHeight * (1-StructurePresentation.BankPanelHeight), Is.GreaterThanOrEqualTo(220));
        }
        [Test] public void HpOffsetIsAboveTerrainAndBelowNodeForBothSeats()
        {
            foreach (int side in new[] { 0, 2 })
            {
                StructurePresentation.GroundOffset(side, 0.6428f, 0.7660f, out float x, out float y, out float z);
                ViewSide.Forward(side, out float fx, out float fz);
                Assert.That(y, Is.GreaterThan(0), "Opaque terrain must not occlude the bar");
                Assert.That(y + (StructurePresentation.PipY - StructurePresentation.PipSize * 0.5f) * 0.6428f,
                    Is.GreaterThan(0), "The lowest edge of the bank pips must also clear terrain");
                float screenUp = y * 0.6428f + (x * fx + z * fz) * 0.7660f;
                Assert.That(screenUp, Is.EqualTo(-StructurePresentation.OffsetDistance).Within(0.0001f));
            }
        }
        [TestCase(540, 1200)]
        [TestCase(1200, 800)]
        public void HudFitsBothScreenDimensions(int width, int height)
        {
            float scale = StructurePresentation.HudScale(width, height, 390, 844);
            Assert.That(390 * scale, Is.LessThanOrEqualTo(width + 0.001f));
            Assert.That(844 * scale, Is.LessThanOrEqualTo(height + 0.001f));
            Assert.That(height / scale - StructurePresentation.BankSheetHeight(DistrictType.Forge, true),
                Is.GreaterThan(300), "The board must remain visible above a contested Forge sheet");
            if (width < height) Assert.That(scale, Is.EqualTo(width / 390f), "Keep the portrait sizing");
        }
    }
}
