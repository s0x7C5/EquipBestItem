using System;
using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>One weight slider row in the slot settings popup.</summary>
public sealed class ParamRowVM : ViewModel
{
    private readonly Action _onValueChanged;
    private float _value;

    public ParamRowVM(ItemParam param, string name, float value, Action onValueChanged)
    {
        Param = param;
        Name = name;
        _value = value;
        _onValueChanged = onValueChanged;
    }

    public ItemParam Param { get; }

    [DataSourceProperty]
    public string Name { get; }

    [DataSourceProperty]
    public float Value
    {
        get => _value;
        set
        {
            if (Math.Abs(value - _value) < 0.001f) return;

            _value = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChanged(nameof(ValueText));
            _onValueChanged();
        }
    }

    [DataSourceProperty]
    public string ValueText => _value.ToString("0.00");
}
