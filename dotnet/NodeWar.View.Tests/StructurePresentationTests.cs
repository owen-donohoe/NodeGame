using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.View.Tests
{
    public class StructurePresentationTests
    {
        [Test] public void BarReadsSharedMechanism()
        {
            var b = GameBalanceData.Default();
            var n = new NodeData { structureKind = StructureKind.Minion, structureHP = 8 };
            Assert.That(StructurePresentation.Fill(n, b), Is.EqualTo(0.5f));
            n.structureKind = StructureKind.Fortification; n.districtType = DistrictType.Fortress;
            Assert.That(StructurePresentation.Fill(n, b), Is.EqualTo(0.5f));
            Assert.That(StructurePresentation.Visible(n, b), Is.True);
            n = StructureRules.Destroy(n);
            Assert.That(StructurePresentation.Visible(n, b), Is.False);
            Assert.That(StructurePresentation.MinionBadge(n), Is.False);
            Assert.That(StructurePresentation.PipResource(n, 0), Is.EqualTo(-1));
        }
        [Test] public void StorehouseFallbackHasDistinctLabelAndIncome()
        {
            var d = DistrictFallback.Describe(DistrictType.Storehouse);
            Assert.That((d.Monogram, d.Name), Is.EqualTo(("S", "Storehouse")));
            var n = new NodeData { structureKind = StructureKind.Minion, bankFood = 2, bankMaterials = 2, bankMetal = 1 };
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
        [Test] public void DestroyedStructureClearsArtWithoutWritingState()
        {
            var b = GameBalanceData.Default();
            var n = new NodeData { districtType = DistrictType.Farm, structureKind = StructureKind.Minion, structureHP = 16, bankFood = 3 };
            Assert.That(StructurePresentation.PipCount(n), Is.EqualTo(3));
            Assert.That(StructurePresentation.Visible(n, b), Is.True);
            Assert.That((n.structureKind, n.structureHP, n.bankFood), Is.EqualTo((StructureKind.Minion, 16, 3)));
            n = StructureRules.Destroy(n);
            Assert.That((StructurePresentation.Visible(n, b), StructurePresentation.PipCount(n), StructurePresentation.MinionBadge(n)),
                Is.EqualTo((false, 0, false)));
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
    }
}
