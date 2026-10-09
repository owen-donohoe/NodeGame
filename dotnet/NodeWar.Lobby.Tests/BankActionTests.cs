using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.UI;
namespace NodeWar.Lobby.Tests
{
    public class BankActionTests
    {
        private static SimulationState State(GameBalanceData b) => new SimulationState {
            tickCount=123, nodes=new[] { new NodeData { nodeID=0, ownerID=0, districtType=DistrictType.Storehouse,
                claimBar=b.claimThreshold, bankFood=5, collectProgress=10 } },
            villagers=new VillagerData[0], players=new[] { new PlayerData { metal=3 }, new PlayerData() } };
        [Test] public void CollectionOnlyQueuesCommands()
        {
            var b=GameBalanceData.Default(); var s=State(b); var m=new BankActionModel();
            m.Describe(s,b,0,0); int hash=SimulationStateHasher.ComputeHash(s);
            foreach (bool start in new[] {true,false}) {
                Assert.That(m.TryCollect(s,b,0,0,start,out var c),Is.True);
                Assert.That((c.type,c.value,c.issuedOnTick,c.villagerID,c.playerID,c.targetNodeID),Is.EqualTo(((CommandType)9,start?1:0,123,-1,0,0)));
            }
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash));
            s.nodes[0].bankFood=0; m.Describe(s,b,0,0); hash=SimulationStateHasher.ComputeHash(s);
            Assert.That(m.TryCollect(s,b,0,0,true,out _),Is.False);
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash));
            s.nodes[0].bankFood=1;
            s.nodes[0].ownerID=1; Assert.That(m.TryCollect(s,b,0,0,true,out _),Is.False);
            s.nodes[0].ownerID=0; m.Describe(s,b,0,0);
            var copy=new SimulationState(); copy.CopyFrom(s); s.CopyFrom(copy);
            Assert.That(m.TryCollect(s,b,0,0,true,out _),Is.False);
            m.Describe(s,b,0,0); s.tickCount=122; Assert.That(m.TryCollect(s,b,0,0,true,out _),Is.False);
        }
        [Test] public void LockAndStorageReasonsMatchSimulation()
        {
            var b=GameBalanceData.Default(); var s=State(b); var m=new BankActionModel();
            foreach(int fixture in new[] {0,1,2,3,4}) {
                s=State(b); s.nodes[0].collectRequested=true;
                if(fixture==0) s.villagers=new[] { new VillagerData { ownerID=1,currentNodeID=0,hp=1,state=VillagerState.Idle } };
                if(fixture==1) s.nodes[0].claimBar--;
                if(fixture==2) s.nodes[0].ownerID=-1;
                if(fixture==3) s.players[0].food=b.foodCap;
                if(fixture==4) { s.nodes[0].bankFood=0; s.players[0].metal=2; }
                var d=m.Describe(s,b,0,0);
                Assert.That(d.Status,Is.EqualTo(BankRules.CollectionState(s,s.nodes[0],b).ToString()));
                if(fixture==3) Assert.That(d.Status,Is.EqualTo("StorageFull"));
                if(fixture==4) Assert.That(d.Status,Is.EqualTo("Empty"));
            }
        }
        [Test] public void StorehouseReportsHealthProductionAndExactLockReason()
        {
            var b=GameBalanceData.Default(); var s=State(b); var m=new BankActionModel();
            var d=m.Describe(s,b,0,0);
            StringAssert.Contains("Health 0 / 3000",d.Information);
            StringAssert.Contains("Production disabled",d.Information);
            s.nodes[0].districtHealth=3000;
            StringAssert.Contains("Bank full",m.Describe(s,b,0,0).Information);
            s.nodes[0].bankFood=1;
            StringAssert.Contains("Production active",m.Describe(s,b,0,0).Information);
            s.villagers=new[] {new VillagerData {ownerID=1,currentNodeID=0,hp=1}};
            StringAssert.Contains("EnemyPresent",m.Describe(s,b,0,0).Information);
            s.villagers=new VillagerData[0]; s.nodes[0].claimBar--;
            StringAssert.Contains("ClaimNotFull",m.Describe(s,b,0,0).Information);
        }
    }
}
