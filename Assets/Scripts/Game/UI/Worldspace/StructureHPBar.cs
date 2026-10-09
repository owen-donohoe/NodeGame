using UnityEngine;
using NodeWar.Simulation;
using NodeWar.View;
namespace NodeWar.UI
{
    /// <summary>Runtime-only sibling of district GFX: no prefab or SortingGroup owns this art.</summary>
    public sealed class StructureHPBar : MonoBehaviour
    {
        private SimulationState state;
        private GameBalanceData balance;
        private int node;
        private float scale;
        private Camera viewCamera;
        private Transform root;
        private SpriteRenderer back,fill,badge;
        private SpriteRenderer[] pips;
        private static Sprite[] sprites;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSprites() { sprites=null; }
        private static Sprite SpriteFor(int shape)
        {
            if(sprites==null) sprites=new Sprite[5];
            if(sprites[shape]!=null) return sprites[shape];
            var tex=new Texture2D(16,16,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave };
            var pixels=new Color32[256];
            for(int y=0;y<16;y++) for(int x=0;x<16;x++) {
                // Food circle, materials square, metal diamond; badge is a cross.
                bool on=StructurePresentation.ShapePixel(shape,x,y);
                if(on) pixels[y*16+x]=new Color32(255,255,255,255);
            }
            tex.SetPixels32(pixels); tex.Apply(false,true);
            var sprite=Sprite.Create(tex,new Rect(0,0,16,16),new Vector2(0.5f,0.5f),16);
            sprite.hideFlags=HideFlags.HideAndDontSave; return sprites[shape]=sprite;
        }
        private SpriteRenderer Quad(string name,int order)
        {
            var go=new GameObject(name); go.transform.SetParent(root,false);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=SpriteFor(0);
            sr.sortingLayerID=SortingLayer.NameToID("Villagers"); sr.sortingOrder=order; return sr;
        }
        public void Initialize(SimulationState s,int id,GameBalanceData b,float nodeScale)
        {
            state=s; node=id; balance=b; scale=nodeScale; viewCamera=Camera.main;
            root=new GameObject("StructureHPBar").transform; root.SetParent(transform,false);
            var parent=transform.lossyScale;
            root.localScale=new Vector3(parent.x!=0?1/parent.x:1,parent.y!=0?1/parent.y:1,parent.z!=0?1/parent.z:1);
            back=Quad("Back",StructurePresentation.BackOrder); back.color=new Color(0.05f,0.05f,0.08f,0.85f);
            back.transform.localScale=new Vector3(StructurePresentation.Width*scale,StructurePresentation.Height*scale,1);
            fill=Quad("HP",StructurePresentation.FillOrder); fill.color=Color.green;
            badge=Quad("Minion",StructurePresentation.FillOrder); badge.sprite=SpriteFor(4); badge.color=Color.white;
            badge.transform.localPosition=new Vector3(StructurePresentation.BadgeX*scale,0,0);
            badge.transform.localScale=new Vector3(StructurePresentation.PipSize*scale,StructurePresentation.PipSize*scale,1);
            pips=new SpriteRenderer[StructurePresentation.MaxPips];
            for(int i=0;i<pips.Length;i++) { pips[i]=Quad("Bank"+i,StructurePresentation.FillOrder);
                pips[i].transform.localScale=new Vector3(StructurePresentation.PipSize*scale,StructurePresentation.PipSize*scale,1); }
            Update();
        }
        private void Update()
        {
            if(root==null || state==null || node<0 || node>=state.nodes.Length) return;
            var n=state.nodes[node]; bool visible=StructurePresentation.Visible(n,balance);
            root.gameObject.SetActive(visible); if(!visible) return;
            float width=StructurePresentation.Width*scale, fraction=StructurePresentation.Fill(n,balance);
            fill.transform.localScale=new Vector3(width*fraction,StructurePresentation.Height*scale*0.7f,1);
            fill.transform.localPosition=new Vector3(-width*(1-fraction)*0.5f,0,0);
            badge.enabled=StructurePresentation.MinionBadge(n);
            int count=StructurePresentation.PipCount(n);
            for(int i=0;i<pips.Length;i++) {
                int resource=StructurePresentation.PipResource(n,i); pips[i].enabled=resource>=0; if(resource<0) continue;
                pips[i].sprite=SpriteFor(resource+1); pips[i].color=resource==0?new Color(0.4f,0.9f,0.3f):resource==1?new Color(0.8f,0.6f,0.3f):new Color(0.65f,0.8f,1);
                pips[i].transform.localPosition=new Vector3(StructurePresentation.PipX(i,count)*scale,StructurePresentation.PipY*scale,0);
            }
        }
        private void LateUpdate()
        {
            if(root==null) return; if(viewCamera==null) viewCamera=Camera.main; if(viewCamera==null) return;
            root.rotation=viewCamera.transform.rotation;
            int side=ViewSide.FromYaw(viewCamera.transform.eulerAngles.y);
            float horizontal=new Vector2(viewCamera.transform.up.x,viewCamera.transform.up.z).magnitude;
            StructurePresentation.GroundOffset(side,viewCamera.transform.up.y,horizontal,out float x,out float y,out float z);
            root.position=transform.position+new Vector3(x,y,z)*scale;
        }
    }
}
