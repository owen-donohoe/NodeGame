using NodeWar.Simulation;
namespace NodeWar.UI
{
    public struct NodeActionDescription
    {
        public int Price, ReadyTick, RemainingTicks, Level, MaterialsCost, MetalCost;
        public bool AutoRecruit, CanRecruit, CanForge, CanRepeat, CanMaterials, CanMetal;
        public string Refusal, Information;
    }

    /// <summary>Read-only node actions. Describe binds a presentation; CopyFrom rollback replaces
    /// the node array, invalidating that binding even if replay reaches the same tick.</summary>
    public sealed class NodeActionModel
    {
        private SimulationState boundState;
        private NodeData[] boundNodes;
        private int boundNode=-1, boundPlayer=-1, boundOwner, boundTick;
        private DistrictType boundType;

        private static bool Valid(SimulationState s, GameBalanceData b, int player, int node) =>
            s != null && s.nodes != null && s.players != null && s.villagers != null &&
            player >= 0 && player < 2 && player < s.players.Length && node >= 0 && node < s.nodes.Length;

        public NodeActionDescription Describe(SimulationState s, GameBalanceData b, int player, int node)
        {
            boundState=null;
            if (!Valid(s,b,player,node)) return new NodeActionDescription { Refusal="InvalidBinding" };
            var n=s.nodes[node];
            boundState=s; boundNodes=s.nodes; boundNode=node; boundPlayer=player;
            boundOwner=n.ownerID; boundType=n.districtType; boundTick=s.tickCount;
            var d=new NodeActionDescription { ReadyTick=n.recruitReadyTick,
                RemainingTicks=(int)System.Math.Min(int.MaxValue,System.Math.Max(0L,(long)n.recruitReadyTick-s.tickCount)),
                AutoRecruit=n.autoRecruit, Level=n.fortressLevel, Refusal="None" };
            if(n.districtType==DistrictType.Village)
            {
                b.TryRecruitCostAndCooldown(s.players[player].recruitCount,s.tickCount,out d.Price,out _);
                d.CanRecruit=NodeActionRules.CanRecruit(s,b,player,node,out var refusal);
                d.Refusal=refusal==RecruitRefusal.CostAboveFoodCap ? "CostExceedsStorage" : refusal.ToString();
                d.CanRepeat=NodeActionRules.CanSetAutoRecruit(s,player,node,n.autoRecruit?0:1);
                d.Information="Recruit: "+d.Price+" food. Ready tick "+d.ReadyTick+" ("+d.RemainingTicks+" ticks remaining). "+d.Refusal;
            }
            else if(n.districtType==DistrictType.Workshop)
            {
                d.Price=b.minionMetalCost;
                d.CanForge=NodeActionRules.CanForgeMinion(s,b,player,node,out var refusal);
                d.Refusal=refusal.ToString();
                d.Information="Forge Minion: "+d.Price+" metal. Ready tick "+d.ReadyTick+" ("+d.RemainingTicks+" ticks remaining). "+d.Refusal;
            }
            else if(n.districtType==DistrictType.Fortress)
            {
                var stats=b.GetDistrictStats(n.districtType,n.districtEra);
                if(GameBalanceData.FortressStatsValid(stats) && n.fortressLevel>=0 && n.fortressLevel<3)
                { d.MaterialsCost=stats.fortressMaterialsCosts[n.fortressLevel+1]; d.MetalCost=stats.fortressMetalCosts[n.fortressLevel+1]; }
                d.CanMaterials=NodeActionRules.CanUpgradeFortress(s,b,player,node,0);
                d.CanMetal=NodeActionRules.CanUpgradeFortress(s,b,player,node,1);
                d.Refusal=d.CanMaterials || d.CanMetal ? "None" : "UpgradeUnavailable";
                d.Information="Fortress level "+d.Level+" / 3. Next: "+d.MaterialsCost+" materials or "+d.MetalCost+" metal. "+
                    NodeWar.View.StructurePresentation.HealthInformation(n,b,"Resistance aura");
            }
            else if(n.districtType==DistrictType.Town)
            {
                d.Information="Town grants "+b.GetDistrictStats(n.districtType,n.districtEra).townBonusVillagers+
                    " villagers once per player. Player 0: "+((n.townPaidMask&1)!=0?"paid":"unpaid")+
                    "; Player 1: "+((n.townPaidMask&2)!=0?"paid":"unpaid")+".";
            }
            else if(n.districtType==DistrictType.Infirmary)
            {
                var stats=b.GetDistrictStats(n.districtType,n.districtEra);
                int workers=NodeWar.View.StructurePresentation.ActiveInfirmaryWorkers(s,node,b);
                d.Information="Local healing every "+stats.healIntervalTicks+" ticks. "+workers+
                    " Acolyte workers assist Core respawns: "+(workers*stats.respawnBoostPerWorker)+
                    " extra timer ticks; "+(workers*stats.respawnCostReductionPercent)+"% food discount. "+
                    NodeWar.View.StructurePresentation.HealthInformation(n,b,"Healing and respawn assistance");
            }
            return d;
        }
        private bool Current(SimulationState s, GameBalanceData b,int player,int node) =>
            Valid(s,b,player,node) && ReferenceEquals(s,boundState) && ReferenceEquals(s.nodes,boundNodes) &&
            player==boundPlayer && node==boundNode && s.tickCount>=boundTick &&
            s.nodes[node].ownerID==boundOwner && s.nodes[node].districtType==boundType;
        private static GameCommand Command(SimulationState s,int player,int node,CommandType type,int value) =>
            new GameCommand { type=type, playerID=player, targetNodeID=node, villagerID=-1, issuedOnTick=s.tickCount, value=value };
        public bool TryRecruit(SimulationState s,GameBalanceData b,int player,int node,out GameCommand c)
        {
            c=default; if(!Current(s,b,player,node) || !NodeActionRules.CanRecruit(s,b,player,node,out _)) return false;
            c=Command(s,player,node,CommandType.Recruit,0); return true;
        }
        public bool TryForgeMinion(SimulationState s,GameBalanceData b,int player,int node,out GameCommand c)
        {
            c=default; if(!Current(s,b,player,node) || !NodeActionRules.CanForgeMinion(s,b,player,node,out _)) return false;
            c=Command(s,player,node,CommandType.ForgeMinion,0); return true;
        }
        public bool TrySetAutoRecruit(SimulationState s,GameBalanceData b,int player,int node,bool desired,out GameCommand c)
        {
            c=default; if(!Current(s,b,player,node) || !NodeActionRules.CanSetAutoRecruit(s,player,node,desired?1:0)) return false;
            c=Command(s,player,node,CommandType.SetAutoRecruit,desired?1:0); return true;
        }
        public bool TryUpgradeFortress(SimulationState s,GameBalanceData b,int player,int node,int currency,out GameCommand c)
        {
            c=default; if(!Current(s,b,player,node) || !NodeActionRules.CanUpgradeFortress(s,b,player,node,currency)) return false;
            c=Command(s,player,node,CommandType.UpgradeFortress,currency); return true;
        }
    }
}
