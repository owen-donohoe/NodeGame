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
        // Reserve room for the fixed action bar as well as a readable scrolling body.
        public static int BankSheetHeight(DistrictType type, bool claim) => !(type == DistrictType.Farm || type == DistrictType.Mine || type == DistrictType.Forge || type == DistrictType.Storehouse) ? 0 :
            (type == DistrictType.Forge ? 430 : 360) + (claim ? 44 : 0);
        // The authored Forge controls reach 220px below the top; the bank needs 136px.
        public static float ForgeContentHeight => Math.Max(220f / (1f-BankPanelHeight), 136f / BankPanelHeight);
        public static float HudScale(int width, int height, int referenceWidth, int referenceHeight) =>
            width > 0 && height > 0 && referenceWidth > 0 && referenceHeight > 0
                ? Math.Min((float)width / referenceWidth, (float)height / referenceHeight) : 1f;
        public static bool Visible(NodeData n,GameBalanceData b) => BankRules.Total(n)>0;
        public static bool HealthVisible(NodeData n,GameBalanceData b) => false;
        public static bool MinionBadge(NodeData n) => false;
        public static float Fill(NodeData n,GameBalanceData b) => 0f;
        public static int PipCount(NodeData n) => (int)Math.Max(0L,Math.Min(MaxPips,BankRules.Total(n)));
        public static int PipResource(NodeData n,int i) => i<0 || i>=PipCount(n) ? -1 : i<n.bankFood ? 0 : i<n.bankFood+n.bankMaterials ? 1 : 2;
        public static float PipX(int i,int count) => (i-(count-1)*0.5f)*PipGap;
        public static bool WorkerPresentation(DistrictType type) => type!=DistrictType.Storehouse;
        public static void Offset(int side,out float x,out float z) { ViewSide.Forward(side,out x,out z); x*=OffsetDistance; z*=OffsetDistance; }
        public static void GroundOffset(int side, float cameraUpY, float cameraUpHorizontal, out float x, out float y, out float z)
        {
            // A camera-up offset alone puts the bar underground, where opaque terrain hides it.
            y = 0.05f + Math.Max(0f, -PipY * cameraUpY) + PipSize * 0.5f * Math.Abs(cameraUpY);
            float distance = (OffsetDistance + y * cameraUpY) / Math.Max(0.01f, cameraUpHorizontal);
            ViewSide.Forward(side, out x, out z);
            x *= -distance; z *= -distance;
        }
        public static bool ShapePixel(int shape,int x,int y) => shape==0 || shape==2 ||
            (shape==1 && (x-7.5f)*(x-7.5f)+(y-7.5f)*(y-7.5f)<=56f) ||
            (shape==3 && Math.Abs(x-7.5f)+Math.Abs(y-7.5f)<=7.5f) ||
            (shape==4 && (x>=6&&x<=9 || y>=6&&y<=9));
    }
}
