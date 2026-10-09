using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;
namespace NodeWar.UI
{
    /// <summary>Runtime controls for districts without an authored content prefab.</summary>
    public sealed class NodeActionPanelContent : MonoBehaviour
    {
        private readonly NodeActionModel model=new NodeActionModel();
        private readonly BankActionModel bankModel=new BankActionModel();
        private Button collect,install;
        private bool requested;
        private SimulationState state;
        private InputBuffer input;
        private GameBalanceData balance;
        private System.Func<int> viewer;
        private int node;
        private bool shownAutoRecruit;
        private TextMeshProUGUI information;
        private Button recruit,repeat,materials,metal;
        public void Unbind() { input=null; enabled=false; }
        public void Initialize(SimulationState s,InputBuffer buffer,NodeWar.Core.ITickProvider ticks,GameBalanceData b,int nodeID,System.Func<int> controlledPlayer)
        {
            state=s; input=buffer; balance=b; node=nodeID; viewer=controlledPlayer;
            var layout=gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight=true; layout.childForceExpandHeight=false;
            information=Text("Information");
            recruit=Control("Recruit",() => { if(model.TryRecruit(state,balance,viewer(),node,out var c)) input.EnqueueCommand(c); Refresh(); });
            repeat=Control("Repeat",() => { if(model.TrySetAutoRecruit(state,balance,viewer(),node,!shownAutoRecruit,out var c)) input.EnqueueCommand(c); Refresh(); });
            materials=Control("Materials",() => Upgrade(0)); metal=Control("Metal",() => Upgrade(1));
            collect=Control("Collect",() => { if(input!=null && isActiveAndEnabled && bankModel.TryCollect(state,balance,viewer(),node,!requested,out var c)) input.EnqueueCommand(c); Refresh(); });
            install=Control("Install",() => { if(input!=null && isActiveAndEnabled && bankModel.TryInstall(state,balance,viewer(),node,out var c)) input.EnqueueCommand(c); Refresh(); });
            Refresh();
        }
        private TextMeshProUGUI Text(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI),typeof(LayoutElement)); go.transform.SetParent(transform,false);
            go.GetComponent<LayoutElement>().preferredHeight=52;
            var text=go.GetComponent<TextMeshProUGUI>(); text.fontSize=18; text.color=Color.white; text.raycastTarget=false;
            return text;
        }
        private Button Control(string name,UnityEngine.Events.UnityAction click)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement)); go.transform.SetParent(transform,false);
            go.GetComponent<LayoutElement>().preferredHeight=42; go.GetComponent<Image>().color=new Color(0.2f,0.25f,0.3f);
            var label=Text(name+" label"); label.transform.SetParent(go.transform,false);
            var rect=label.rectTransform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            var button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>(); button.onClick.AddListener(click); return button;
        }
        private void Upgrade(int currency)
        { if(model.TryUpgradeFortress(state,balance,viewer(),node,currency,out var c)) input.EnqueueCommand(c); Refresh(); }
        private static void Set(Button button,string text,bool visible,bool enabled)
        { button.gameObject.SetActive(visible); button.interactable=enabled; button.GetComponentInChildren<TextMeshProUGUI>().text=text; }
        public void Refresh()
        {
            if(input==null || state==null || node<0 || node>=state.nodes.Length) return;
            int player=viewer(); var d=model.Describe(state,balance,player,node); information.text=d.Information; shownAutoRecruit=d.AutoRecruit;
            bool yours=state.nodes[node].ownerID==player;
            bool village=state.nodes[node].districtType==DistrictType.Village;
            bool fortress=state.nodes[node].districtType==DistrictType.Fortress;
            Set(recruit,"Recruit - "+d.Price+" food",yours&&village,d.CanRecruit);
            Set(repeat,"Repeat: "+(d.AutoRecruit?"On":"Off"),yours&&village,d.CanRepeat);
            Set(materials,"Upgrade - "+d.MaterialsCost+" materials",yours&&fortress,d.CanMaterials);
            Set(metal,"Upgrade - "+d.MetalCost+" metal",yours&&fortress,d.CanMetal);
            var bank=bankModel.Describe(state,balance,player,node); requested=bank.Requested;
            if(bank.Visible) information.text=bank.Information;
            Set(collect,bank.CollectLabel,bank.Visible&&bank.ShowActions,bank.CanCollect);
            Set(install,bank.InstallLabel,bank.Visible&&bank.ShowActions,bank.CanInstall);
        }
    }
}

