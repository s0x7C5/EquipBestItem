using Bannerlord.UIExtenderEx;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace Bannerlord.EquipBestItem
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                var uiExtender = UIExtender.Create("Bannerlord.EquipBestItem");
                uiExtender.Register(typeof(SubModule).Assembly);
                uiExtender.Enable();
            }
            catch (MBException exception)
            {
                Helper.ShowMessage($"EquipBestItem failed to apply UIExtender patches {exception.Message}", Colors.Red);
            }
        }
    }
}