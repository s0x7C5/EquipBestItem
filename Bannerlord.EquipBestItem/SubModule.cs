using System;
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.EquipBestItem;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        try
        {
            ModRuntime.Initialize();

            var uiExtender = UIExtender.Create("Bannerlord.EquipBestItem");
            uiExtender.Register(typeof(SubModule).Assembly);
            uiExtender.Enable();
        }
        catch (Exception exception)
        {
            GameLog.Error($"failed to initialize: {exception.Message}");
        }
    }

    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        MainThread.Drain();
    }
}
