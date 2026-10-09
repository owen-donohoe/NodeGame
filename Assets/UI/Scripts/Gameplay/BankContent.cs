using UnityEngine.UIElements;
namespace NodeWar.UI
{
    /// <summary>Bank controls embedded in both production and Forge sheets.</summary>
    public sealed class BankContent : NodeSheetContent
    {
        private readonly BankActionModel model=new BankActionModel();
        private Label information;
        private Button collect,install;
        private bool requested;
        public override ResourceKind InvolvedResources => ResourceKind.Food | ResourceKind.Materials | ResourceKind.Metal;
        protected override void OnBind()
        {
            information=Caption(""); Root.Add(information);
            collect=PrimaryButton(() => { if(model.TryCollect(State,Balance,ControlledPID,NodeID,!requested,out var c)) Send(c); Refresh(); });
            install=PrimaryButton(() => { if(model.TryInstall(State,Balance,ControlledPID,NodeID,out var c)) Send(c); Refresh(); });
            Actions.Add(collect); Actions.Add(install);
        }
        public override void Refresh()
        {
            var d=model.Describe(State,Balance,ControlledPID,NodeID);
            information.text=d.Information; requested=d.Requested;
            Show(information,d.Visible); Show(collect,d.Visible&&d.ShowActions); Show(install,false);
            collect.text=d.CollectLabel; collect.SetEnabled(d.CanCollect);
            install.text=d.InstallLabel; install.SetEnabled(d.CanInstall);
        }
    }
}
