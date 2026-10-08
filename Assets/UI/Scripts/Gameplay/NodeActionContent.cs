using UnityEngine.UIElements;
using NodeWar.Simulation;
namespace NodeWar.UI
{
    public sealed class NodeActionContent : NodeSheetContent
    {
        private readonly NodeActionModel model=new NodeActionModel();
        private Label information;
        private Button recruit, materials, metal;
        private Toggle repeat;
        protected override int LayoutKey => (int)State.nodes[NodeID].districtType;
        public override ResourceKind InvolvedResources => State.nodes[NodeID].districtType==DistrictType.Village
            ? ResourceKind.Food : State.nodes[NodeID].districtType==DistrictType.Fortress ? ResourceKind.Materials | ResourceKind.Metal : ResourceKind.None;
        protected override void OnBind()
        {
            information=Caption(""); Root.Add(information);
            recruit=PrimaryButton(() => { if(model.TryRecruit(State,Balance,ControlledPID,NodeID,out var c)) Send(c); Refresh(); });
            repeat=new Toggle("Repeat");
            repeat.RegisterValueChangedCallback(e => { if(model.TrySetAutoRecruit(State,Balance,ControlledPID,NodeID,e.newValue,out var c)) Send(c); Refresh(); });
            materials=PrimaryButton(() => Upgrade(0)); metal=PrimaryButton(() => Upgrade(1));
            Actions.Add(recruit); Actions.Add(repeat); Actions.Add(materials); Actions.Add(metal);
        }
        private void Upgrade(int currency)
        { if(model.TryUpgradeFortress(State,Balance,ControlledPID,NodeID,currency,out var c)) Send(c); Refresh(); }
        public override void Refresh()
        {
            var d=model.Describe(State,Balance,ControlledPID,NodeID);
            information.text=d.Information;
            bool yours=State.nodes[NodeID].ownerID==ControlledPID;
            bool village=State.nodes[NodeID].districtType==DistrictType.Village;
            bool fortress=State.nodes[NodeID].districtType==DistrictType.Fortress;
            Show(recruit,yours&&village); Show(repeat,yours&&village); Show(materials,yours&&fortress); Show(metal,yours&&fortress);
            recruit.text="Recruit - "+d.Price+" food"; recruit.SetEnabled(d.CanRecruit);
            repeat.SetValueWithoutNotify(d.AutoRecruit); repeat.SetEnabled(d.CanRepeat);
            materials.text="Upgrade - "+d.MaterialsCost+" materials"; materials.SetEnabled(d.CanMaterials);
            metal.text="Upgrade - "+d.MetalCost+" metal"; metal.SetEnabled(d.CanMetal);
        }
    }
}
