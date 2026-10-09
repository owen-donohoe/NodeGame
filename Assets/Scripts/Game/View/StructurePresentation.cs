using System;
using NodeWar.Simulation;
namespace NodeWar.View
{
    /// <summary>Read-only layout for code-built structure and bank art.</summary>
    public static class StructurePresentation
    {
        public const int BackOrder=402, FillOrder=403, MaxPips=5;
        public const float Width=0.7f, Height=0.09f, PipSize=0.08f, PipGap=0.12f, PipY=-0.14f, BadgeX=-0.46f;
        public const float OffsetDistance=0.62f, BankPanelHeight=0.45f;
        public static bool Visible(NodeData n,GameBalanceData b) => n.structureKind!=StructureKind.None && n.structureHP>0 && StructureRules.MaxHP(n,b)>0;
        public static bool MinionBadge(NodeData n) => n.structureKind==StructureKind.Minion && n.structureHP>0;
        public static float Fill(NodeData n,GameBalanceData b) => StructureRules.MaxHP(n,b)>0 ? Math.Max(0f,Math.Min(1f,(float)n.structureHP/StructureRules.MaxHP(n,b))) : 0f;
        public static int PipCount(NodeData n) => n.structureKind==StructureKind.Minion ? (int)Math.Max(0L,Math.Min(MaxPips,BankRules.Total(n))) : 0;
        public static int PipResource(NodeData n,int i) => i<0 || i>=PipCount(n) ? -1 : i<n.bankFood ? 0 : i<n.bankFood+n.bankMaterials ? 1 : 2;
        public static float PipX(int i,int count) => (i-(count-1)*0.5f)*PipGap;
        public static bool WorkerPresentation(DistrictType type) => type!=DistrictType.Storehouse;
        public static void Offset(int side,out float x,out float z) { ViewSide.Forward(side,out x,out z); x*=OffsetDistance; z*=OffsetDistance; }
        public static bool ShapePixel(int shape,int x,int y) => shape==0 || shape==2 ||
            (shape==1 && (x-7.5f)*(x-7.5f)+(y-7.5f)*(y-7.5f)<=56f) ||
            (shape==3 && Math.Abs(x-7.5f)+Math.Abs(y-7.5f)<=7.5f) ||
            (shape==4 && (x>=6&&x<=9 || y>=6&&y<=9));
    }
}
