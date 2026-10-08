using System.Collections.Generic;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.UI;
namespace NodeWar.Lobby.Tests
{
    public class NodeActionTests
    {
        private static SimulationState State(DistrictType type = DistrictType.Village) => new SimulationState
        {
            tickCount = 150, nodes = new[] { new NodeData { nodeID=0, ownerID=0, districtType=type, links=new Link[0], recruitReadyTick=160 } },
            villagers = new VillagerData[0], players = new[] { new PlayerData { playerID=0, food=9, materials=8, metal=2, recruitCount=1 }, new PlayerData { playerID=1 } }
        };
        [Test] public void RecruitUiShowsExactPriceReadyTickAndRefusal()
        {
            var s=State(); var b=GameBalanceData.Default(); var m=new NodeActionModel();
            var d=m.Describe(s,b,0,0);
            Assert.That((d.Price,d.ReadyTick,d.RemainingTicks,d.CanRecruit,d.Refusal), Is.EqualTo((9,160,10,false,"Cooldown")));
            s.tickCount=160; Assert.That(m.Describe(s,b,0,0).CanRecruit, Is.True);
            s.players[0].recruitCount=9; s.players[0].food=30;
            d=m.Describe(s,b,0,0); Assert.That((d.CanRecruit,d.Refusal),Is.EqualTo((false,"CostExceedsStorage")));
            s.players[0].recruitCount=8; Assert.That(m.Describe(s,b,0,0).CanRecruit,Is.True);
        }
        private static void Check(GameCommand c, CommandType type, int value, int tick)
        {
            Assert.That((c.type,c.playerID,c.targetNodeID,c.villagerID,c.issuedOnTick,c.value),Is.EqualTo((type,0,0,-1,tick,value)));
        }
        [Test] public void ControlsQueueCommandsNeverMutateState()
        {
            var s=State(); s.tickCount=160; var b=GameBalanceData.Default(); var m=new NodeActionModel(); var sink=new List<GameCommand>();
            m.Describe(s,b,0,0); int hash=SimulationStateHasher.ComputeHash(s);
            Assert.That(m.TryRecruit(s,b,0,0,out var c),Is.True); sink.Add(c); Check(c,CommandType.Recruit,0,160);
            Assert.That(m.TrySetAutoRecruit(s,b,0,0,true,out c),Is.True); sink.Add(c); Check(c,CommandType.SetAutoRecruit,1,160);
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash)); Assert.That(sink.Count,Is.EqualTo(2));
            s.nodes[0].ownerID=1; Assert.That(m.TryRecruit(s,b,0,0,out c),Is.False);
            s.nodes[0].ownerID=0; var copy=new SimulationState(); copy.CopyFrom(s); s.CopyFrom(copy);
            Assert.That(m.TryRecruit(s,b,0,0,out c),Is.False);
            m.Describe(s,b,0,0); Assert.That(m.TryRecruit(s,b,0,0,out c),Is.True);
            Assert.That(m.TryRecruit(s,b,1,0,out c),Is.False);
        }
        [Test] public void RepeatToggle_SendsAbsoluteDesiredValue()
        {
            var s=State(); var b=GameBalanceData.Default(); var m=new NodeActionModel(); var sink=new List<GameCommand>();
            foreach(bool current in new[]{false,true})
            {
                s.nodes[0].autoRecruit=current; m.Describe(s,b,0,0); int hash=SimulationStateHasher.ComputeHash(s);
                Assert.That(m.TrySetAutoRecruit(s,b,0,0,!current,out var c),Is.True); sink.Add(c);
                Check(c,CommandType.SetAutoRecruit,current?0:1,150); Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash));
            }
            Assert.That(sink.Count,Is.EqualTo(2));
        }
        [Test] public void UpgradeUi_ReportsBothCurrencies()
        {
            var s=State(DistrictType.Fortress); s.nodes[0].fortressLevel=1; var b=GameBalanceData.Default(); var m=new NodeActionModel();
            var d=m.Describe(s,b,0,0); Assert.That((d.Level,d.MaterialsCost,d.MetalCost,d.CanMaterials,d.CanMetal),Is.EqualTo((1,8,2,true,true)));
            var sink=new List<GameCommand>(); int hash=SimulationStateHasher.ComputeHash(s);
            foreach(int currency in new[]{0,1}) { Assert.That(m.TryUpgradeFortress(s,b,0,0,currency,out var c),Is.True); sink.Add(c); Check(c,CommandType.UpgradeFortress,currency,150); }
            Assert.That(sink.Count,Is.EqualTo(2)); Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(hash));
            sink.Clear(); s.nodes[0].ownerID=1; Assert.That(m.TryUpgradeFortress(s,b,0,0,0,out var refused),Is.False);
            d=m.Describe(s,b,0,0); Assert.That(d.CanMaterials || d.CanMetal,Is.False); Assert.That(sink.Count,Is.Zero);
        }
    }
}
