using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Profiles;

/// <summary>
///     Built-in starting weights per slot. Weapon slots default to an empty
///     vector, which makes the search fall back to the game's Effectiveness
///     score — weapons are too heterogeneous for a fixed weight preset.
/// </summary>
public static class DefaultWeights
{
    public static ParamWeights For(EquipmentIndex slot)
    {
        var weights = new ParamWeights();

        switch (slot)
        {
            case EquipmentIndex.Head:
                weights[ItemParam.HeadArmor] = 1f;
                break;
            case EquipmentIndex.Body:
                weights[ItemParam.BodyArmor] = 1f;
                weights[ItemParam.ArmArmor] = 0.2f;
                weights[ItemParam.LegArmor] = 0.2f;
                break;
            case EquipmentIndex.Leg:
                weights[ItemParam.LegArmor] = 1f;
                break;
            case EquipmentIndex.Gloves:
                weights[ItemParam.ArmArmor] = 1f;
                break;
            case EquipmentIndex.Cape:
                weights[ItemParam.BodyArmor] = 1f;
                break;
            case EquipmentIndex.Horse:
                weights[ItemParam.Speed] = 0.6f;
                weights[ItemParam.Maneuver] = 0.6f;
                weights[ItemParam.ChargeDamage] = 0.3f;
                weights[ItemParam.HitPoints] = 0.4f;
                break;
            case EquipmentIndex.HorseHarness:
                weights[ItemParam.MountArmor] = 1f;
                break;
        }

        return weights;
    }
}
