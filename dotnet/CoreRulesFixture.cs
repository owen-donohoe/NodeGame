using System;
using NodeWar.Simulation;
using NUnit.Framework;
namespace NodeWar.Tests
{
    /// <summary>Test-only BOARD copy; never advertises its hash under the shipped map ID.</summary>
    public static class CoreRulesFixture
    {
        public const string MapId="hourglass-01-acceptance";
        public static GameBalanceData Balance => GameBalanceData.Default();
        public static BoardConfigData Board()
        {
            var b=PremadeMaps.Hourglass01();
            b.startingFood=30; b.startingMaterials=30; b.startingMetal=10;
            var p=new System.Collections.Generic.List<BoardConfigData.InitialDistrictPlacement>(b.initialPlacements);
            p.Add(Placement(4,4,DistrictType.Village,0,10000));
            p.Add(Placement(5,4,DistrictType.Fortress,0,9500));
            p.Add(Placement(4,3,DistrictType.Town,-1,9800));
            p.Add(Placement(1,2,DistrictType.Town,-1,-9600));
            b.initialPlacements=p.ToArray(); return b;
        }
        private static BoardConfigData.InitialDistrictPlacement Placement(int x,int z,DistrictType type,int owner,int bar) =>
            new BoardConfigData.InitialDistrictPlacement {gridX=x,gridZ=z,districtType=type,ownerID=owner,claimBar=bar};
        public static readonly DraftPlacement[] Draft={new DraftPlacement {playerID=0,districtType=DistrictType.Farm,gridX=4,gridZ=2}};
        public static PlayerSetup[] Players() => new[]
        {
            new PlayerSetup {suits=new[]{(int)SuitType.Warrior},districts=new int[0]},
            new PlayerSetup {suits=new[]{(int)SuitType.Warrior},districts=new int[0]}
        };
        public static SimulationState NewState()
        { var b=Board(); MatchFactory.Configure(Balance,b); return MatchFactory.Build(Balance,b,Draft,Players()); }
        public static GameCommand[] Script(int player,int tick)
        {
            if(player==0 && tick==2) return new[] { Action(CommandType.Recruit,11,0,tick), Action(CommandType.UpgradeFortress,12,0,tick),
                Move(0,0,6,tick), Move(1,0,8,tick), Move(2,0,12,tick) };
            if(player==0 && tick==3) return new[] {Action(CommandType.SetAutoRecruit,11,1,tick),Action(CommandType.UpgradeFortress,12,1,tick)};
            if(player==0 && tick==4) return new[] {Action(CommandType.SetAutoRecruit,11,0,tick)};
            if(player==1 && tick==2) return new[] {Move(3,1,5,tick),Move(4,1,8,tick)};
            return null;
        }
        private static GameCommand Action(CommandType type,int node,int value,int tick) =>
            new GameCommand {type=type,playerID=0,targetNodeID=node,villagerID=-1,issuedOnTick=tick,value=value};
        private static GameCommand Move(int villager,int player,int node,int tick) =>
            new GameCommand {type=CommandType.Move,playerID=player,targetNodeID=node,villagerID=villager,issuedOnTick=tick,value=0};
        public static GameCommand[] Commands(int tick)
        {
            var all=new System.Collections.Generic.List<GameCommand>();
            for(int p=0;p<2;p++) {var c=Script(p,tick); if(c!=null) all.AddRange(c);}
            return all.ToArray();
        }
        public sealed class Witness
        {
            private bool recruit,on,off,materials,metal,restore,fight,resume,town0,town1,frontier;
            public void Command(SimulationState s,GameCommand c)
            {
                int food=s.players[c.playerID].food, mat=s.players[c.playerID].materials, met=s.players[c.playerID].metal;
                int population=NodeActionRules.CountPopulation(s,c.playerID), count=s.players[c.playerID].recruitCount;
                CommandProcessor.ProcessCommand(s,c);
                if(c.type==CommandType.Recruit)
                {
                    Assert.That(s.players[0].food,Is.EqualTo(food-6)); Assert.That(s.players[0].recruitCount,Is.EqualTo(count+1));
                    Assert.That(NodeActionRules.CountPopulation(s,0),Is.EqualTo(population+1)); Assert.That(s.nodes[11].recruitReadyTick,Is.EqualTo(c.issuedOnTick+60)); recruit=true;
                }
                if(c.type==CommandType.SetAutoRecruit) {Assert.That(s.nodes[11].autoRecruit,Is.EqualTo(c.value==1)); if(c.value==1) on=true; else off=true;}
                if(c.type==CommandType.UpgradeFortress)
                {
                    if(c.value==0) {Assert.That(s.players[0].materials,Is.EqualTo(mat-4)); Assert.That(s.nodes[12].fortressLevel,Is.EqualTo(1)); materials=true;}
                    else {Assert.That(s.players[0].metal,Is.EqualTo(met-2)); Assert.That(s.nodes[12].fortressLevel,Is.EqualTo(2)); metal=true;}
                }
            }
            public void Tick(SimulationState s)
            {
                var before=new SimulationState(); before.CopyFrom(s); GameSimulation.SimulateTick(s);
                if(before.nodes[12].claimBar<10000 && s.nodes[12].claimBar>before.nodes[12].claimBar)
                {
                    Assert.That(s.nodes[12].claimBar,Is.EqualTo(Math.Min(10000,before.nodes[12].claimBar+17)));
                    Assert.That(s.nodes[12].ownerID,Is.EqualTo(0)); restore=true;
                }
                if(s.villagers[0].state==VillagerState.Fighting)
                {Assert.That(s.villagers[0].targetNodeID,Is.EqualTo(6)); fight=true;}
                if(fight && before.villagers[0].state==VillagerState.Fighting && s.villagers[0].state==VillagerState.Moving)
                {Assert.That(s.villagers[0].targetNodeID,Is.EqualTo(6)); Assert.That(s.villagers[0].hp,Is.GreaterThan(0)); resume=true;}
                                if(before.nodes[6].ownerID==-1 && s.nodes[6].claimBar>before.nodes[6].claimBar)
                {
                    Assert.That(s.nodes[6].claimBar,Is.EqualTo(Math.Min(10000,before.nodes[6].claimBar+21)),"17 * 125 / 100 frontier bonus"); frontier=true;
                }
                foreach(int node in new[]{5,8})
                {
                    if(s.nodes[node].townPaidMask==before.nodes[node].townPaidMask) continue;
                    int player=s.nodes[node].ownerID;
                    Assert.That(s.nodes[node].townPaidMask,Is.EqualTo(before.nodes[node].townPaidMask|(1<<player)));
                    Assert.That(NodeActionRules.CountPopulation(s,player),Is.EqualTo(NodeActionRules.CountPopulation(before,player)+2));
                    Assert.That(s.players[player].recruitCount,Is.EqualTo(before.players[player].recruitCount));
                    if(player==0) town0=true; else town1=true;
                }
            }
            public void Complete()
            {
                Assert.That((recruit,on,off,materials,metal,restore,fight,resume,town0,town1,frontier),
                    Is.EqualTo((true,true,true,true,true,true,true,true,true,true,true)),"Every required C1-C7 oracle must be witnessed.");
            }
        }
        public static int[] Population(SimulationState s)
        {
            var result=new int[3+s.villagers.Length*3];
            result[0]=s.villagers.Length; result[1]=NodeActionRules.CountPopulation(s,0); result[2]=NodeActionRules.CountPopulation(s,1);
            for(int i=0;i<s.villagers.Length;i++) {result[3+i*3]=s.villagers[i].villagerID; result[4+i*3]=s.villagers[i].ownerID; result[5+i*3]=s.villagers[i].isConsumed?1:0;}
            return result;
        }
        public static SimulationState Reference(out int[] hashes,out int[][] populations)
        {
            var s=NewState(); var w=new Witness(); hashes=new int[1501]; populations=new int[1501][];
            for(int tick=0;tick<1500;tick++)
            {
                foreach(var c in Commands(tick)) w.Command(s,c); w.Tick(s);
                hashes[s.tickCount]=SimulationStateHasher.ComputeHash(s);
                populations[s.tickCount]=Population(s);
            }
            Assert.That(s.tickCount,Is.EqualTo(1500)); Assert.That(s.gameOver,Is.False); w.Complete(); return s;
        }
    }
}


