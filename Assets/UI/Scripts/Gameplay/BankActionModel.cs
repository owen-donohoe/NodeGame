using NodeWar.Simulation;
namespace NodeWar.UI
{
    public struct BankActionDescription
    {
        public bool Visible, ShowActions, CanCollect, CanInstall, Requested, Workers;
        public string Information, Status, Refusal, CollectLabel, InstallLabel;
    }
    /// <summary>Both node sheets use this binding and the simulation's eligibility queries.</summary>
    public sealed class BankActionModel
    {
        private SimulationState boundState;
        private NodeData[] boundNodes;
        private int boundNode, boundPlayer, boundOwner, boundTick;
        private DistrictType boundType;
        private static GameCommand Command(SimulationState s,int p,int n,CommandType t,int v) => new GameCommand {
            type=t,playerID=p,targetNodeID=n,villagerID=-1,value=v,issuedOnTick=s.tickCount };
        public BankActionDescription Describe(SimulationState s,GameBalanceData b,int p,int n)
        {
            boundState=null;
            if(s==null || s.nodes==null || s.players==null || s.villagers==null || p<0 || p>1 || p>=s.players.Length || n<0 || n>=s.nodes.Length) return default;
            var node=s.nodes[n]; boundState=s; boundNodes=s.nodes; boundNode=n; boundPlayer=p;
            boundOwner=node.ownerID; boundType=node.districtType; boundTick=s.tickCount;
            string status=BankRules.CollectionState(s,node,b).ToString();
            string refusal=BankRules.InstallReason(s,b,p,n).ToString();
            int duration=BankRules.ProductionTicks(node,b);
            return new BankActionDescription { Visible=BankRules.MinionDistrict(node.districtType), ShowActions=node.ownerID==p,
                CanCollect=BankRules.CanCollect(s,Command(s,p,n,CommandType.Collect,node.collectRequested?0:1)),
                CanInstall=BankRules.CanInstallMinion(s,b,Command(s,p,n,CommandType.InstallMinion,0)),
                Requested=node.collectRequested, Workers=NodeWar.View.StructurePresentation.WorkerPresentation(node.districtType),
                Status=status, Refusal=refusal, CollectLabel=node.collectRequested?"Cancel collection":"Collect",
                InstallLabel="Install Minion - "+b.minionMetalCost+" metal",
                Information="Bank "+BankRules.Total(node)+" / "+b.bankCapacity+": "+node.bankFood+" food, "+node.bankMaterials+" materials, "+node.bankMetal+" metal. Collection "+node.collectProgress+" / 16: "+status+
                    ". Minion: "+refusal+". Production "+node.minionProductionRemaining+" / "+duration+" ticks remaining." };
        }
        private bool Current(SimulationState s,int p,int n) => ReferenceEquals(s,boundState) && s!=null && ReferenceEquals(s.nodes,boundNodes) &&
            n==boundNode && p==boundPlayer && s.tickCount>=boundTick && s.nodes[n].ownerID==boundOwner && s.nodes[n].districtType==boundType;
        public bool TryCollect(SimulationState s,GameBalanceData b,int p,int n,bool start,out GameCommand c)
        { c=default; if(!Current(s,p,n)) return false; var candidate=Command(s,p,n,CommandType.Collect,start?1:0); if(!BankRules.CanCollect(s,candidate)) return false; c=candidate; return true; }
        public bool TryInstall(SimulationState s,GameBalanceData b,int p,int n,out GameCommand c)
        { c=default; if(!Current(s,p,n)) return false; var candidate=Command(s,p,n,CommandType.InstallMinion,0); if(!BankRules.CanInstallMinion(s,b,candidate)) return false; c=candidate; return true; }
    }
}
