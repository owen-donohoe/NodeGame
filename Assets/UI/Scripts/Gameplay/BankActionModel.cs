using NodeWar.Simulation;
namespace NodeWar.UI
{
    public struct BankActionDescription
    {
        public bool Visible, ShowActions, CanCollect, Requested, Workers;
        public string Information, Status, Refusal, CollectLabel;
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
            string refusal=node.ownerID<0 ? "Neutral" :
                NodeActionRules.HasLivingEnemyAtNode(s,node.ownerID,n) ? "EnemyPresent" :
                BankRules.Locked(s,node,b) ? "ClaimNotFull" : "";
            int duration=BankRules.ProductionTicks(node,b);
            string production="";
            if(node.districtType==DistrictType.Storehouse)
            {
                production=" "+NodeWar.View.StructurePresentation.HealthInformation(node,b,"Production")+
                    (BankRules.Total(node)>=b.bankCapacity ? " Bank full." : "")+
                    " Alternates +1 food and +1 materials every "+duration+" ticks; "+node.bankProductionRemaining+" ticks remaining.";
            }
            return new BankActionDescription { Visible=(node.districtType==DistrictType.Storehouse || BankRules.Total(node)>0), ShowActions=node.ownerID==p,
                CanCollect=BankRules.CanCollect(s,Command(s,p,n,CommandType.Collect,node.collectRequested?0:1)),
                Requested=node.collectRequested, Workers=NodeWar.View.StructurePresentation.WorkerPresentation(node.districtType),
                Status=status, Refusal=refusal, CollectLabel=node.collectRequested?"Cancel collection":"Collect",
                Information="Bank "+BankRules.Total(node)+" / "+b.bankCapacity+": "+node.bankFood+" food, "+node.bankMaterials+" materials, "+node.bankMetal+" metal. Collection "+node.collectProgress+" / "+b.collectProgressPerUnit+": "+status+
                    (refusal.Length>0 ? " ("+refusal+")" : "")+"."+production };
        }
        private bool Current(SimulationState s,int p,int n) => ReferenceEquals(s,boundState) && s!=null && ReferenceEquals(s.nodes,boundNodes) &&
            n==boundNode && p==boundPlayer && s.tickCount>=boundTick && s.nodes[n].ownerID==boundOwner && s.nodes[n].districtType==boundType;
        public bool TryCollect(SimulationState s,GameBalanceData b,int p,int n,bool start,out GameCommand c)
        { c=default; if(!Current(s,p,n)) return false; var candidate=Command(s,p,n,CommandType.Collect,start?1:0); if(!BankRules.CanCollect(s,candidate)) return false; c=candidate; return true; }
    }
}
