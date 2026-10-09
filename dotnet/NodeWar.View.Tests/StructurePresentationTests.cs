using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.View.Tests
{
    public class StructurePresentationTests
    {
        [TestCase(DistrictType.Fortress)]
        [TestCase(DistrictType.Storehouse)]
        [TestCase(DistrictType.Infirmary)]
        public void HealthExtensionHidesAtFullAndTracksDamageForBothSeats(DistrictType type)
        {
            var b=GameBalanceData.Default();
            var n=new NodeData { districtType=type, ownerID=0 };
            int max=b.GetDistrictStats(type,0).healthMax;
            foreach(int side in new[] {0,2}) foreach(int owner in new[] {0,1})
            {
                n.ownerID=owner; n.districtHealth=max;
                Assert.That(StructurePresentation.HealthSegment(n,b,100,10).Visible,Is.False);
                n.districtHealth=max/2;
                var layout=StructurePresentation.HealthSegment(n,b,100,10);
                Assert.That(layout.Visible,Is.True);
                Assert.That(layout.Fraction,Is.EqualTo(0.5f));
                Assert.That(layout.EndSign,Is.EqualTo(owner==0?1:-1));
                Assert.That(layout.X*layout.EndSign-layout.Width/2,Is.GreaterThan(50));
                Assert.That(layout.FillWidth,Is.EqualTo(layout.Width*0.5f));
                Assert.That(layout.FillX,Is.EqualTo(-layout.EndSign*layout.Width*0.25f));
                Assert.That(StructurePresentation.HealthWorldX(layout,side),Is.EqualTo(side==0?layout.X:-layout.X));
                n.districtHealth=0;
                Assert.That(StructurePresentation.HealthSegment(n,b,100,10).Visible,Is.True);
                Assert.That(StructurePresentation.HealthSegment(n,b,100,10).Fraction,Is.Zero);
            }
            n.ownerID=-1;
            Assert.That(StructurePresentation.HealthSegment(n,b,100,10).Visible,Is.False);
        }

        [TestCase(DistrictType.Workshop)]
        [TestCase(DistrictType.Village)]
        [TestCase(DistrictType.Farm)]
        public void NoHealthDistrictNeverShowsExtension(DistrictType type)
        {
            var n=new NodeData { districtType=type, ownerID=0 };
            var layout=StructurePresentation.HealthSegment(n,GameBalanceData.Default(),100,10);
            Assert.That(layout.Visible,Is.False); Assert.That(layout.Fraction,Is.Zero);
        }

        [Test] public void PipsOnlyShowStorehouseBanksAndResourceShapesDiffer()
        {
            var n=new NodeData { districtType=DistrictType.Storehouse };
            Assert.That(StructurePresentation.PipCount(n),Is.Zero);
            n.bankFood=1; n.bankMaterials=1; n.bankMetal=1;
            Assert.That(StructurePresentation.PipCount(n),Is.EqualTo(3));
            Assert.That(new[] {StructurePresentation.PipResource(n,0),StructurePresentation.PipResource(n,1),StructurePresentation.PipResource(n,2)},Is.EqualTo(new[] {0,1,2}));
            Assert.That(StructurePresentation.ShapePixel(1,0,0),Is.False);
            Assert.That(StructurePresentation.ShapePixel(2,0,0),Is.True);
            Assert.That(StructurePresentation.ShapePixel(3,0,7),Is.False);
            var tints=new System.Collections.Generic.HashSet<(float,float,float)>();
            for(int resource=0;resource<3;resource++)
            {
                StructurePresentation.ResourceTint(resource,out float r,out float g,out float b);
                Assert.That(tints.Add((r,g,b)),Is.True,"Each resource needs its own colour as well as shape");
            }
            n.districtType=DistrictType.Farm;
            Assert.That(StructurePresentation.PipCount(n),Is.Zero);
        }

        [Test] public void MinionTintAndLabelDoNotDependOnSeatOrActivity()
        {
            foreach(SuitType suit in System.Enum.GetValues(typeof(SuitType)))
            {
                bool tinted=StructurePresentation.TrySuitTint(suit,out float r,out float g,out float b);
                Assert.That(tinted,Is.EqualTo(suit==SuitType.Minion));
                if(tinted) Assert.That((r,g,b),Is.EqualTo((0.6f,0.6f,0.6f)));
            }
            Assert.That(StructurePresentation.SuitLabel(SuitType.Minion),Is.EqualTo("Minion"));
        }

        [TestCase(844)] [TestCase(600)] [TestCase(320)]
        public void StorehouseSheetIsVerticallyCentredAndOtherSheetsStayDocked(int viewport)
        {
            int height=StructurePresentation.BankSheetHeight(DistrictType.Storehouse,false);
            float bottom=StructurePresentation.SheetBottom(DistrictType.Storehouse,viewport,height);
            Assert.That(bottom,Is.EqualTo(System.Math.Max(0,(viewport-height)*0.5f)));
            if(viewport>=height) Assert.That(bottom+height*0.5f,Is.EqualTo(viewport*0.5f));
            Assert.That(StructurePresentation.SheetBottom(DistrictType.Forge,viewport,height),Is.Zero);
            foreach(float pivot in new[] {0f,0.5f,1f}) foreach(float anchorMax in new[] {0f,0.2f,1f})
            {
                float anchored=StructurePresentation.SheetAnchoredY(viewport,height,pivot,0f,anchorMax);
                Assert.That(anchored+viewport*anchorMax*pivot-pivot*height,Is.EqualTo(bottom).Within(0.001f));
            }
        }
        [TestCase(540, 1200, false)]
        [TestCase(540, 1200, true)]
        [TestCase(1200, 800, false)]
        [TestCase(1200, 800, true)]
        public void StorehouseCentreUsesTargetHeightAcrossResizeAndClaimTransitions(int width, int height, bool claim)
        {
            float viewport = height / StructurePresentation.HudScale(width, height, 390, 844);
            int targetHeight = StructurePresentation.BankSheetHeight(DistrictType.Storehouse, claim);
            float bottom = StructurePresentation.BankSheetBottom(DistrictType.Storehouse, viewport, claim);
            Assert.That(bottom + targetHeight / 2f, Is.EqualTo(viewport / 2f).Within(0.001f));
            Assert.That(StructurePresentation.BankSheetBottom(DistrictType.Workshop, viewport, claim), Is.Zero);
            Assert.That(StructurePresentation.BankSheetBottom(DistrictType.Storehouse, 0, claim), Is.Zero);
            Assert.That(StructurePresentation.BankSheetBottom(DistrictType.Storehouse, 200, claim), Is.Zero);
            float otherBottom = StructurePresentation.BankSheetBottom(DistrictType.Storehouse, viewport, !claim);
            Assert.That(bottom - otherBottom, Is.EqualTo(claim ? -22f : 22f).Within(0.001f));
        }

        [Test] public void PassiveEffectsOnlyReturnAtFullHealthAndInfirmaryWorkersMatchRules()
        {
            var b=GameBalanceData.Default();
            var s=new SimulationState { nodes=new[] {new NodeData {districtType=DistrictType.Infirmary,ownerID=0}},
                villagers=new[] {
                    new VillagerData {ownerID=0,currentNodeID=0,suit=SuitType.Acolyte,state=VillagerState.Working,hp=1},
                    new VillagerData {ownerID=0,currentNodeID=0,suit=SuitType.Acolyte,state=VillagerState.Working,hp=1},
                    new VillagerData {ownerID=0,currentNodeID=0,suit=SuitType.Acolyte,state=VillagerState.Working,hp=1},
                    new VillagerData {ownerID=1,currentNodeID=0,suit=SuitType.Minion,state=VillagerState.Idle,hp=1} } };
            foreach(int health in new[] {0,1500,2999,3000})
            {
                s.nodes[0].districtHealth=health;
                Assert.That(StructurePresentation.EffectActive(s.nodes[0],b),Is.EqualTo(health==3000));
                Assert.That(StructurePresentation.ActiveInfirmaryWorkers(s,0,b),Is.EqualTo(health==3000?2:0));
            }
            s.villagers[3].suit=SuitType.None;
            Assert.That(StructurePresentation.ActiveInfirmaryWorkers(s,0,b),Is.Zero);
            s.nodes[0].ownerID=-1;
            Assert.That(StructurePresentation.EffectActive(s.nodes[0],b),Is.False);
        }

        [Test] public void StorehouseFallbackHasDistinctLabelAndIncome()
        {
            var d = DistrictFallback.Describe(DistrictType.Storehouse);
            Assert.That((d.Monogram, d.Name), Is.EqualTo(("S", "Storehouse")));
            var n = new NodeData { districtType=DistrictType.Storehouse, bankFood = 2, bankMaterials = 2, bankMetal = 1 };
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
