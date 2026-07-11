using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.EquipBestItem;

public sealed class SubModule : MBSubModuleBase
{
    private bool _mcmRegistered;

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

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();

        if (_mcmRegistered) return;
        _mcmRegistered = true;

        try
        {
            // MCM is an optional module: only touch its types (in a separate,
            // non-inlined method) when it is actually loaded, so running
            // without MCM never triggers a type load of the settings facade.
            if (TaleWorlds.Engine.Utilities.GetModulesNames().Contains("Bannerlord.MBOptionScreen"))
                RegisterMcmSettings();
        }
        catch (Exception exception)
        {
            GameLog.Error($"MCM registration failed: {exception.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterMcmSettings()
    {
        _ = Settings.McmSettings.Instance;
    }

    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        MainThread.Drain();
    }
}
