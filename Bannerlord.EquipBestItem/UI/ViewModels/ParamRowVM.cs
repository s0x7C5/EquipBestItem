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
            _onValueChanged();
        }
    }

    /// <summary>
    ///     The parameter's signed share of the total scoring weight — only
    ///     shares matter to the scorer, not absolute values. Recomputed by
    ///     the popup whenever any slider moves.
    /// </summary>
    [DataSourceProperty]
    public string ValueText => _shareText;

    private string _shareText = "0%";

    internal void UpdateShare(float denominator)
    {
        var share = denominator > 0f ? (int)Math.Round(_value / denominator * 100f) : 0;
        var text = share + "%";
        if (text == _shareText) return;

        _shareText = text;
        OnPropertyChanged(nameof(ValueText));
    }
}
