using System;
using NodeWar.Simulation;
using NUnit.Framework;
namespace NodeWar.Tests
{
    /// <summary>Test-only hourglass copy for the D6 acceptance script; never advertises its hash under the shipped map ID.</summary>
    public static class BankRulesFixture
    {
        public const string MapId="hourglass-01-banks-acceptance";
        public static GameBalanceData Balance
        {
            get
            {
                var b=GameBalanceData.Default();
                // A small explicit health reserve lets this bounded script witness collection before the raid.
                for (int i=0;i<b.districtStats.Length;i++)
                    if (b.districtStats[i].districtType==DistrictType.Storehouse) b.districtStats[i].healthMax=170;
                b.suitStats=new[] { new SuitStats {suitType=SuitType.Warrior,era=0,bonusHP=5,attackDamage=2,moveSpeedTicks=16,attackCooldownMax=10,foodCost=2,materialCost=2,fightPriority=1} };
                return b;
            }
        }
        public static BoardConfigData Board()
        {
            var b=CoreRulesFixture.Board();
            var p=new System.Collections.Generic.List<BoardConfigData.InitialDistrictPlacement>(b.initialPlacements);
            p.Add(Placement(2,1,DistrictType.Barracks,1,-10000));
            p.Add(Placement(5,5,DistrictType.Storehouse,0,10000));
            b.initialPlacements=p.ToArray(); return b;
        }
        private static BoardConfigData.InitialDistrictPlacement Placement(int x,int z,DistrictType type,int owner,int bar) =>
            new BoardConfigData.InitialDistrictPlacement {gridX=x,gridZ=z,districtType=type,ownerID=owner,claimBar=bar};
        public static readonly DraftPlacement[] Draft={CoreRulesFixture.Draft[0],new DraftPlacement {playerID=1,districtType=DistrictType.Pier,gridX=1,gridZ=3}};
        public const int Pier=8, TownP0=9, Town=9, Bank=18, Village=12, Fortress=13, BarracksNode=1;
        private static GameCommand Cmd(CommandType type,int player,int node,int villager,int value,int tick) =>
            new GameCommand {type=type,playerID=player,targetNodeID=node,villagerID=villager,issuedOnTick=tick,value=value};
        public static GameCommand[] Script(int player,int tick)
        {
            if(player==0)
                switch(tick)
                {
                    case 2: return new[] {Cmd(CommandType.Recruit,0,Village,-1,0,tick),
                        Cmd(CommandType.UpgradeFortress,0,Fortress,-1,0,tick),Cmd(CommandType.Move,0,Town,1,0,tick)};
                    case 3: return new[] {Cmd(CommandType.SetAutoRecruit,0,Village,-1,1,tick),Cmd(CommandType.UpgradeFortress,0,Fortress,-1,1,tick)};
                    case 4: return new[] {Cmd(CommandType.SetAutoRecruit,0,Village,-1,0,tick)};
                    case 66: case 130: case 200: return new[] {Cmd(CommandType.Recruit,0,Village,-1,0,tick)};
                    case 305: return new[] {Cmd(CommandType.Collect,0,Bank,-1,1,tick)};
                    case 720: return new[] {Cmd(CommandType.Move,0,17,1,0,tick)};
                    case 845: return new[] {Cmd(CommandType.Move,0,Pier,1,0,tick)};
                    case 330: return new[] {Cmd(CommandType.Move,0,Bank,1,0,tick)};
                    case 700: return new[] {Cmd(CommandType.Move,0,Pier,0,0,tick),Cmd(CommandType.Move,0,Pier,2,0,tick)};
                    case 880: return new[] {Cmd(CommandType.Move,0,5,0,0,tick),Cmd(CommandType.Move,0,5,2,0,tick)};
                }
            else
                switch(tick)
                {
                    case 2: return new[] {Cmd(CommandType.Move,1,BarracksNode,3,0,tick),Cmd(CommandType.Move,1,BarracksNode,4,0,tick),Cmd(CommandType.Move,1,Pier,5,0,tick)};
                    case 850: return new[] {Cmd(CommandType.Equip,1,BarracksNode,3,(int)SuitType.Warrior,tick),Cmd(CommandType.Equip,1,BarracksNode,4,(int)SuitType.Warrior,tick)};
                    case 200: case 900: return new[] {Cmd(CommandType.Move,1,Bank,3,0,tick),Cmd(CommandType.Move,1,Bank,4,0,tick)};
                    case 905: return new[] {Cmd(CommandType.Move,1,Pier,5,0,tick)};
                    case 340: return new[] {Cmd(CommandType.Move,1,17,3,0,tick),Cmd(CommandType.Move,1,17,4,0,tick)};
                    case 660: return new[] {Cmd(CommandType.Move,1,BarracksNode,3,0,tick),Cmd(CommandType.Move,1,BarracksNode,4,0,tick)};
                    case 810: return new[] {Cmd(CommandType.Move,1,BarracksNode,3,0,tick),Cmd(CommandType.Move,1,BarracksNode,4,0,tick)};
                }
            return null;
        }
        public static GameCommand[] Commands(int tick)
        {
            var all=new System.Collections.Generic.List<GameCommand>();
            for(int p=0;p<2;p++) {var c=Script(p,tick); if(c!=null) all.AddRange(c);}
            return all.ToArray();
        }
        public sealed class Witness
        {
            public bool Produced,Partial,Paused,Restored,Drained,Gate,Neutralised,MidLeg,Recruited,Upgraded,TownPaid;
            public void Command(SimulationState s,GameCommand c)
            {
                int metal=s.players[c.playerID].metal, food=s.players[c.playerID].food, count=s.players[c.playerID].recruitCount;
                CommandProcessor.ProcessCommand(s,c);
                if(c.type==CommandType.Collect && c.value==1) Assert.That(s.nodes[Bank].collectRequested,Is.EqualTo(BankRules.Total(s.nodes[Bank])>0));
                if(c.type==CommandType.Recruit && s.players[0].recruitCount==count+1) {Assert.That(s.players[0].food,Is.LessThan(food)); Recruited=true;}
                if(c.type==CommandType.UpgradeFortress && c.value==1) Upgraded=s.nodes[Fortress].fortressLevel==2;
            }
            public void Tick(SimulationState s,GameBalanceData balance)
            {
                var before=new SimulationState(); before.CopyFrom(s); GameSimulation.SimulateTick(s);
                NodeData a=before.nodes[Bank], b=s.nodes[Bank];
                bool lockedBefore=BankRules.Locked(before,a,balance);
                if(a.ownerID==0 && b.ownerID==0 && a.collectRequested && BankRules.Total(a)>0 && lockedBefore && BankRules.Locked(s,b,balance))
                {Assert.That(b.collectProgress,Is.EqualTo(a.collectProgress),"a locked bank pauses its dwell clock"); Assert.That(b.bankFood,Is.GreaterThanOrEqualTo(a.bankFood)); Paused=true;}
                if(a.ownerID==0 && b.ownerID==0 && a.collectRequested && lockedBefore && !BankRules.Locked(s,b,balance) && BankRules.Total(b)>0) Restored=true;
                if(b.collectRequested && a.collectRequested && b.bankFood<a.bankFood && BankRules.Total(b)>0)
                {Assert.That(s.players[0].food,Is.EqualTo(Math.Min(balance.foodCap,before.players[0].food+1))); Partial=true;}
                if (b.ownerID == 0 && b.bankFood + b.bankMaterials > a.bankFood + a.bankMaterials) Produced=true;
                if (a.ownerID == 0 && b.ownerID == 0 && b.districtHealth < a.districtHealth) Drained=true;
                NodeData pa=before.nodes[Pier], pb=s.nodes[Pier];
                for(int i=0;i<before.villagers.Length;i++)
                {
                    var v=before.villagers[i];
                    if(v.hp>0 && !v.isConsumed && v.state!=VillagerState.Moving && v.targetNodeID>=0 && v.targetNodeID!=v.currentNodeID && PierGate.IsEnemyPier(before,v)) Gate=true;
                    if(v.state==VillagerState.Moving && v.moveLegDurationTicks>0 && pa.ownerID!=pb.ownerID && v.movePath!=null && Array.IndexOf(v.movePath,Pier)>=0) MidLeg=true;
                }
                if(pa.ownerID==1 && pb.ownerID==-1) Neutralised=true;
                if(before.nodes[TownP0].townPaidMask!=s.nodes[TownP0].townPaidMask) TownPaid=true;
            }
            public void Complete()
            {
                Assert.That((Produced,Partial,Paused,Restored,Drained,Gate,Neutralised,MidLeg,Recruited,Upgraded,TownPaid),
                    Is.EqualTo((true,true,true,true,true,true,true,true,true,true,true)),"Every D oracle must be witnessed: "+Describe());
            }
            private string Describe() => $"produced={Produced} partial={Partial} paused={Paused} restored={Restored} drained={Drained} gate={Gate} neutralised={Neutralised} midLeg={MidLeg} recruit={Recruited} fortress={Upgraded} town={TownPaid}";
        }
        public static int[] Population(SimulationState s) => CoreRulesFixture.Population(s);
        public static SimulationState Reference(out int[] hashes,out int[][] populations)
        {
            var s=NewState(); var w=new Witness(); var balance=Balance; hashes=new int[1501]; populations=new int[1501][];
            for(int tick=0;tick<1500;tick++)
            {
                foreach(var c in Commands(tick)) w.Command(s,c); w.Tick(s,balance);
                hashes[s.tickCount]=SimulationStateHasher.ComputeHash(s);
                populations[s.tickCount]=Population(s);
            }
            Assert.That(s.tickCount,Is.EqualTo(1500)); Assert.That(s.gameOver,Is.False); w.Complete(); return s;
        }
        public static SimulationState NewState()
        { var b=Board(); MatchFactory.Configure(Balance,b); return MatchFactory.Build(Balance,b,Draft,CoreRulesFixture.Players()); }
    }
}
