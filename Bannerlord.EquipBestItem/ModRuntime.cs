using System;
using System.IO;
using System.Net;
using Bannerlord.EquipBestItem.Ai;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Domain.Filtering;
using Bannerlord.EquipBestItem.Domain.Scoring;
using Bannerlord.EquipBestItem.Inventory;
using Bannerlord.EquipBestItem.Persistence;
using Bannerlord.EquipBestItem.Profiles;
using Bannerlord.EquipBestItem.Settings;

namespace Bannerlord.EquipBestItem;

/// <summary>
///     Composition root. The only static seam in the mod — required because
///     UIExtenderEx instantiates view model mixins reflectively, so services
///     cannot be constructor-injected into that entry point.
/// </summary>
internal static class ModRuntime
{
    internal static ModServices Services { get; private set; } = null!;

    internal static void Initialize()
    {
        // net472 defaults to TLS 1.0 on some systems; LLM endpoints require 1.2+.
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "Configs", "EquipBestItem");

        var store = new JsonFileStore(configDirectory);
        var settings = store.Load("settings.json", () => new ModSettings());
        // Write the file back so players can discover and edit the options.
        store.Save("settings.json", settings);

        var finder = new BestItemFinder(new IItemFilter[]
        {
            new EquippableFilter(),
            new SlotFilter(),
            new WeaponMatchFilter(),
            new ShieldOncePerSetFilter(),
            new QueryConstraintFilter()
        });

        Services = new ModServices(
            settings,
            new ProfileService(store),
            new EquipBestService(finder, new WeightedItemScorer(), new EffectivenessItemScorer()),
            new LlmRequestInterpreter(settings.Ai));
    }
}

/// <summary>Immutable service aggregate handed to the UI layer.</summary>
internal sealed class ModServices
{
    internal ModServices(
        ModSettings settings,
        ProfileService profiles,
        EquipBestService equipBest,
        IRequestInterpreter interpreter)
    {
        Settings = settings;
        Profiles = profiles;
        EquipBest = equipBest;
        Interpreter = interpreter;
    }

    internal ModSettings Settings { get; }

    internal ProfileService Profiles { get; }

    internal EquipBestService EquipBest { get; }

    internal IRequestInterpreter Interpreter { get; }
}
