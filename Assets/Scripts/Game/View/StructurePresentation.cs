using System;
using NodeWar.Simulation;
namespace NodeWar.View
{
    public struct HealthSegmentLayout
    {
        public bool Visible;
        public int EndSign;
        public float Fraction, X, Width, Height, FillX, FillWidth, FillHeight;
    }

    /// <summary>Read-only district health, bank art, suit tint and sheet layout.</summary>
    public static class StructurePresentation
    {
        public const int BackOrder=402, FillOrder=403, MaxPips=5;
        public const float PipSize=0.08f, PipGap=0.12f;
        // Bank pips sit centred above the Storehouse, this far along camera-up (node scales).
        public const float PipLift=0.55f;
        public static void PipAnchor(float upX,float upY,float upZ,out float x,out float y,out float z)
        { x=upX*PipLift; y=upY*PipLift; z=upZ*PipLift; }
        // Reserve room for the fixed action bar as well as a readable scrolling body.
        public static int BankSheetHeight(DistrictType type, bool claim) => type != DistrictType.Storehouse ? 0 : 360 + (claim ? 44 : 0);
        public static float HudScale(int width, int height, int referenceWidth, int referenceHeight) =>
            width > 0 && height > 0 && referenceWidth > 0 && referenceHeight > 0
                ? Math.Min((float)width / referenceWidth, (float)height / referenceHeight) : 1f;
        public static HealthSegmentLayout HealthSegment(NodeData n,GameBalanceData b,float barWidth,float barHeight)
        {
            int max=b.GetDistrictStats(n.districtType,n.districtEra).healthMax;
            int sign=n.ownerID==0 ? 1 : -1;
            float fraction=max>0 ? Math.Max(0f,Math.Min(1f,(float)n.districtHealth/max)) : 0f;
            float width=Math.Max(0f,barWidth)*0.28f;
            return new HealthSegmentLayout {
                Visible=max>0 && n.districtHealth<max && (n.ownerID==0 || n.ownerID==1),
                EndSign=sign, Fraction=fraction, Width=width, Height=Math.Max(0f,barHeight)*1.4f,
                X=sign*(Math.Max(0f,barWidth)*0.53f+width*0.5f),
                FillX=-sign*width*(1f-fraction)*0.5f, FillWidth=width*fraction,
                FillHeight=Math.Max(0f,barHeight)*0.9f };
        }
        // The claim canvas billboards: P0 stays screen-right in either seat.
        public static float HealthWorldX(HealthSegmentLayout layout,int side) =>
            ViewSide.Wrap(side)==2 ? -layout.X : ViewSide.Wrap(side)==0 ? layout.X : 0f;
        public static bool EffectActive(NodeData n,GameBalanceData b) => n.ownerID>=0 &&
            (b.GetDistrictStats(n.districtType,n.districtEra).healthMax==0 ||
             n.districtHealth==b.GetDistrictStats(n.districtType,n.districtEra).healthMax);
        public static string HealthInformation(NodeData n,GameBalanceData b,string effect) =>
            "Health "+n.districtHealth+" / "+b.GetDistrictStats(n.districtType,n.districtEra).healthMax+
            ". "+effect+" "+(EffectActive(n,b)?"active":"disabled until fully healed")+".";
        public static int ActiveInfirmaryWorkers(SimulationState state,int nodeID,GameBalanceData balance)
        {
            var node=state.nodes[nodeID];
            if(node.districtType!=DistrictType.Infirmary || !EffectActive(node,balance)) return 0;
            int count=0;
            for(int i=0;i<state.villagers.Length;i++)
            {
                var v=state.villagers[i];
                if(v.currentNodeID!=nodeID || v.isConsumed || v.state==VillagerState.Dead || v.hp<=0 || !NodeActionRules.IsBody(v)) continue;
                if(v.ownerID!=node.ownerID) return 0;
                if(v.suit==SuitType.Acolyte && v.state==VillagerState.Working && count<2) count++;
            }
            return count;
        }
        public static bool TrySuitTint(SuitType suit,out float r,out float g,out float b)
        { r=g=b=0.6f; return suit==SuitType.Minion; }
        public static string SuitLabel(SuitType suit) => suit==SuitType.Minion ? "Minion" : suit.ToString();
        public static void ResourceTint(int resource,out float r,out float g,out float b)
        {
            r=resource==0?0.4f:resource==1?0.8f:0.65f;
            g=resource==0?0.9f:resource==1?0.6f:0.8f;
            b=resource==0?0.3f:resource==1?0.3f:1f;
        }
        public static int PipCount(NodeData n) => n.districtType!=DistrictType.Storehouse ? 0 : (int)Math.Max(0L,Math.Min(MaxPips,BankRules.Total(n)));
        public static int PipResource(NodeData n,int i) => i<0 || i>=PipCount(n) ? -1 : i<n.bankFood ? 0 : i<n.bankFood+n.bankMaterials ? 1 : 2;
        public static float PipX(int i,int count) => (i-(count-1)*0.5f)*PipGap;
        public static bool WorkerPresentation(DistrictType type) => type!=DistrictType.Storehouse;
        public static bool ShapePixel(int shape,int x,int y) => shape==0 || shape==2 ||
            (shape==1 && (x-7.5f)*(x-7.5f)+(y-7.5f)*(y-7.5f)<=56f) ||
            (shape==3 && Math.Abs(x-7.5f)+Math.Abs(y-7.5f)<=7.5f);
    }
}
