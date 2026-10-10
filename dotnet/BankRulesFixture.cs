using System;
using NodeWar.Simulation;
using NUnit.Framework;
namespace NodeWar.Tests
{
    /// <summary>Test-only hourglass and accelerated E acceptance tuning; never uses the shipped map identity.</summary>
    public static class BankRulesFixture
    {
        public const string MapId="hourglass-01-banks-acceptance";
        public static GameBalanceData Balance
        {
            get
            {
                var b=GameBalanceData.Default();
                // Bound the phases without changing shipped tuning or simulation rules.
                b.baseMoveSpeedTicks=4; b.minionMoveSpeedTicks=2; b.baseClaimPerTick=50;
                for (int i=0;i<b.districtStats.Length;i++)
                {
                    if (b.districtStats[i].districtType==DistrictType.Storehouse || b.districtStats[i].districtType==DistrictType.Fortress) b.districtStats[i].healthMax=170;
                    if (b.districtStats[i].districtType==DistrictType.Storehouse) b.districtStats[i].productionTicks=10;
                }
                b.suitStats=new[] { new SuitStats {suitType=SuitType.Warrior,era=0,bonusHP=5,attackDamage=2,moveSpeedTicks=4,attackCooldownMax=5,foodCost=2,materialCost=2,fightPriority=1} };
                return b;
            }
        }
        public static BoardConfigData Board()
        {
            var b=CoreRulesFixture.Board(); b.defaultLinkWeight=1;
            var p=new System.Collections.Generic.List<BoardConfigData.InitialDistrictPlacement>(b.initialPlacements);
            for(int i=0;i<p.Count;i++)
                if(p[i].districtType==DistrictType.Fortress) {var f=p[i]; f.claimBar=10000; p[i]=f;}
            p.Add(Placement(2,1,DistrictType.Barracks,1,-10000));
            p.Add(Placement(5,5,DistrictType.Storehouse,0,10000));
            p.Add(Placement(4,5,DistrictType.Workshop,0,10000));
            b.initialPlacements=p.ToArray(); return b;
        }
        private static BoardConfigData.InitialDistrictPlacement Placement(int x,int z,DistrictType type,int owner,int bar) =>
            new BoardConfigData.InitialDistrictPlacement {gridX=x,gridZ=z,districtType=type,ownerID=owner,claimBar=bar};
        public static readonly DraftPlacement[] Draft={CoreRulesFixture.Draft[0],new DraftPlacement {playerID=1,districtType=DistrictType.Pier,gridX=1,gridZ=3}};
        public const int Pier=8, TownP0=9, Bank=18, Village=12, Fortress=13, BarracksNode=1, Workshop=17, Minion=7;
        private static GameCommand Cmd(CommandType type,int player,int node,int villager,int value,int tick) =>
            new GameCommand {type=type,playerID=player,targetNodeID=node,villagerID=villager,issuedOnTick=tick,value=value};
        public static GameCommand[] Script(int player,int tick)
        {
            if(player==0)
                switch(tick)
                {
                    case 2: return new[] {Cmd(CommandType.Recruit,0,Village,-1,0,tick),Cmd(CommandType.ForgeMinion,0,Workshop,-1,0,tick),
                        Cmd(CommandType.UpgradeFortress,0,Fortress,-1,0,tick),Cmd(CommandType.Move,0,TownP0,1,0,tick)};
                    case 25: return new[] {Cmd(CommandType.Collect,0,Bank,-1,1,tick)};
                    case 27: return new[] {Cmd(CommandType.Collect,0,Bank,-1,0,tick)};
                    case 45: return new[] {Cmd(CommandType.Move,0,Bank,Minion,0,tick)};
                    case 110: return new[] {Cmd(CommandType.Collect,0,Bank,-1,1,tick)};
                    case 160: return new[] {Cmd(CommandType.Move,0,Bank,0,0,tick),Cmd(CommandType.Move,0,Fortress,2,0,tick)};
                    case 250: return new[] {Cmd(CommandType.Move,0,Pier,1,0,tick)};
                    case 280: return new[] {Cmd(CommandType.Move,0,5,1,0,tick)};
                    case 310: return new[] {Cmd(CommandType.Move,0,Workshop,0,0,tick),Cmd(CommandType.Collect,0,Bank,-1,0,tick)};
                }
            else
                switch(tick)
                {
                    case 2: return new[] {Cmd(CommandType.Move,1,BarracksNode,3,0,tick),Cmd(CommandType.Move,1,BarracksNode,4,0,tick),Cmd(CommandType.Move,1,Pier,5,0,tick)};
                    case 15: return new[] {Cmd(CommandType.Equip,1,BarracksNode,3,(int)SuitType.Warrior,tick),Cmd(CommandType.Equip,1,BarracksNode,4,(int)SuitType.Warrior,tick)};
                    case 80: return new[] {Cmd(CommandType.Move,1,Fortress,3,0,tick),Cmd(CommandType.Move,1,Bank,4,0,tick)};
                    case 125: return new[] {Cmd(CommandType.Move,1,BarracksNode,3,0,tick)};
                    case 145: return new[] {Cmd(CommandType.Move,1,BarracksNode,4,0,tick)};
                    case 350: return new[] {Cmd(CommandType.Move,1,Bank,3,0,tick),Cmd(CommandType.Move,1,Bank,4,0,tick)};
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
            private bool forged, walked, minionCollected, remoteStart, remoteCancel, produced, bankDrained, locked, restored, unlocked, paused;
            private bool auraInitiallyOn, fortressZero, auraOff, barRefilled, regenerated, auraBack, killed, gate, recruited, townPaid;
            private int capturePayouts, deathTick;
            private static int[] Owners(SimulationState s)
            { var owners=new int[s.nodes.Length]; for(int i=0;i<owners.Length;i++) owners[i]=s.nodes[i].ownerID; return owners; }
            public void Command(SimulationState s,GameCommand c)
            {
                int metal=s.players[c.playerID].metal, food=s.players[c.playerID].food, length=s.villagers.Length;
                if(c.type==CommandType.Collect && c.issuedOnTick==27) Assert.That(s.nodes[Bank].collectProgress,Is.GreaterThan(0));
                CommandProcessor.ProcessCommand(s,c);
                if(c.type==CommandType.ForgeMinion)
                {
                    Assert.That(s.nodes[c.targetNodeID].districtType,Is.EqualTo(DistrictType.Workshop));
                    Assert.That(s.villagers.Length,Is.EqualTo(length+1));
                    Assert.That(s.players[0].metal,Is.EqualTo(metal-Balance.minionMetalCost));
                    Assert.That((s.villagers[Minion].suit,s.villagers[Minion].currentNodeID,s.villagers[Minion].hp),
                        Is.EqualTo((SuitType.Minion,Workshop,Balance.minionHP))); forged=true;
                }
                if(c.type==CommandType.Collect && c.issuedOnTick==25)
                {
                    Assert.That(BankRules.HasStationaryCollector(s,s.nodes[Bank]),Is.False);
                    Assert.That(s.nodes[Bank].collectRequested,Is.True); remoteStart=true;
                }
                if(c.type==CommandType.Collect && c.issuedOnTick==27)
                { Assert.That(s.nodes[Bank].collectRequested,Is.False); Assert.That(s.nodes[Bank].collectProgress,Is.Zero); remoteCancel=true; }
                if(c.type==CommandType.Recruit)
                { Assert.That(s.villagers.Length,Is.EqualTo(length+1)); Assert.That(s.players[0].food,Is.EqualTo(food-6)); recruited=true; }
            }
            public void Tick(SimulationState s,GameBalanceData balance)
            {
                var before=new SimulationState(); before.CopyFrom(s); GameSimulation.SimulateTick(s);
                NodeData a=before.nodes[Bank], b=s.nodes[Bank];
                if(b.ownerID==0 && BankRules.Total(b)>BankRules.Total(a)) produced=true;
                if(a.ownerID==0 && b.ownerID==0 && b.districtHealth<a.districtHealth) bankDrained=true;
                if(bankDrained && b.ownerID==0 && NodeActionRules.HasLivingEnemyAtNode(s,0,Bank))
                { Assert.That(BankRules.Locked(s,b,balance),Is.True); locked=true; }
                if(a.ownerID==0 && b.ownerID==0 && a.collectRequested && BankRules.Locked(before,a,balance) && BankRules.Locked(s,b,balance))
                { Assert.That(b.collectProgress,Is.EqualTo(a.collectProgress),"Locked collection pauses its dwell clock."); paused=true; }
                if(locked && a.ownerID==0 && b.ownerID==0 && b.claimBar>a.claimBar)
                { Assert.That(NodeActionRules.HasLivingEnemyAtNode(s,0,Bank),Is.False); restored=true; }
                if(restored && b.ownerID==0 && b.claimBar==balance.claimThreshold && !BankRules.Locked(s,b,balance)) unlocked=true;
                if(s.villagers.Length>Minion)
                {
                    var m=s.villagers[Minion]; var old=before.villagers[Minion];
                    if(m.suit==SuitType.Minion && m.state==VillagerState.Moving && m.targetNodeID==Bank) walked=true;
                    if(walked && old.currentNodeID==Bank && old.state==VillagerState.Idle && !a.collectRequested &&
                        BankRules.Total(b)<BankRules.Total(a) && b.ownerID==0)
                    {
                        Assert.That(s.players[0].food+s.players[0].materials,Is.EqualTo(before.players[0].food+before.players[0].materials+1));
                        minionCollected=true;
                    }
                    if(old.hp>0 && m.hp==0)
                    { Assert.That(old.state,Is.EqualTo(VillagerState.Fighting)); Assert.That(m.isConsumed,Is.True); killed=true; deathTick=s.tickCount; }
                    if(killed) { Assert.That(m.isConsumed,Is.True); Assert.That(m.hp,Is.Zero); Assert.That(m.state,Is.EqualTo(VillagerState.Dead)); }
                }
                var fa=before.nodes[Fortress]; var fb=s.nodes[Fortress];
                GameSimulation.BuildResistanceSnapshot(s,Owners(s),out var resistance,out var sources);
                int max=balance.GetDistrictStats(DistrictType.Fortress,fb.districtEra).healthMax;
                if(!fortressZero && fb.districtHealth==max && resistance[Fortress]>0) auraInitiallyOn=true;
                if(fa.districtHealth>0 && fb.districtHealth==0 && fb.ownerID==0)
                { Assert.That(auraInitiallyOn,Is.True); Assert.That(NodeActionRules.HasLivingEnemyAtNode(s,0,Fortress),Is.True); fortressZero=true; }
                if(fortressZero && fb.districtHealth<max)
                {
                    Assert.That(resistance[Fortress],Is.Zero); Assert.That(sources[Fortress],Is.EqualTo(-1)); auraOff=true;
                    if(fb.claimBar==balance.claimThreshold) barRefilled=true;
                    if(fb.districtHealth>fa.districtHealth)
                    { Assert.That(barRefilled,Is.True); Assert.That(fb.districtHealth,Is.EqualTo(Math.Min(max,fa.districtHealth+balance.GetDistrictStats(DistrictType.Fortress,fb.districtEra).healthRegenPerTick))); regenerated=true; }
                }
                if(fortressZero && regenerated && fb.districtHealth==max)
                { Assert.That(resistance[Fortress],Is.GreaterThan(0)); Assert.That(sources[Fortress],Is.EqualTo(Fortress)); auraBack=true; }
                if(b.ownerID==1 && a.ownerID!=1)
                {
                    Assert.That(BankRules.Total(a),Is.GreaterThan(0),"Capture must loot a remaining bank.");
                    Assert.That((s.players[1].food,s.players[1].materials,s.players[1].metal),Is.EqualTo((
                        Math.Min(balance.foodCap,before.players[1].food+a.bankFood),
                        Math.Min(balance.materialsCap,before.players[1].materials+a.bankMaterials),
                        Math.Min(balance.metalCap,before.players[1].metal+a.bankMetal))));
                    Assert.That(s.players[1].food+s.players[1].materials,Is.GreaterThan(before.players[1].food+before.players[1].materials));
                    Assert.That(BankRules.Total(b),Is.Zero); capturePayouts++;
                }
                // During the regeneration window, no new production or second loot payment can occur.
                if(capturePayouts>0 && a.ownerID==1 && b.ownerID==1 && !DistrictHealth.Healthy(before,a))
                    Assert.That((s.players[1].food,s.players[1].materials,s.players[1].metal),
                        Is.EqualTo((before.players[1].food,before.players[1].materials,before.players[1].metal)));
                foreach(var v in s.villagers)
                    if(v.hp>0 && !v.isConsumed && v.state!=VillagerState.Moving && v.targetNodeID>=0 &&
                        v.targetNodeID!=v.currentNodeID && PierGate.IsEnemyPier(s,v)) gate=true;
                if(before.nodes[TownP0].townPaidMask!=s.nodes[TownP0].townPaidMask)
                {
                    Assert.That(s.nodes[TownP0].townPaidMask,Is.EqualTo(1));
                    Assert.That(NodeActionRules.CountPopulation(s,0),Is.EqualTo(NodeActionRules.CountPopulation(before,0)+2)); townPaid=true;
                }
            }
            public void Complete()
            {
                var required=new[] {(nameof(forged),forged),(nameof(walked),walked),(nameof(minionCollected),minionCollected),
                    (nameof(remoteStart),remoteStart),(nameof(remoteCancel),remoteCancel),(nameof(produced),produced),
                    (nameof(bankDrained),bankDrained),(nameof(locked),locked),(nameof(paused),paused),(nameof(restored),restored),(nameof(unlocked),unlocked),
                    (nameof(auraInitiallyOn),auraInitiallyOn),(nameof(fortressZero),fortressZero),(nameof(auraOff),auraOff),
                    (nameof(barRefilled),barRefilled),(nameof(regenerated),regenerated),(nameof(auraBack),auraBack),
                    (nameof(killed),killed),(nameof(gate),gate),(nameof(recruited),recruited),(nameof(townPaid),townPaid)};
                foreach(var item in required) Assert.That(item.Item2,Is.True,"Missing E witness: "+item.Item1);
                Assert.That(capturePayouts,Is.EqualTo(1));
                Assert.That(1500-deathTick,Is.GreaterThan(Balance.respawnTicks),"Observe the consumed minion beyond its ordinary respawn window.");
            }
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
