using System;
using System.Collections.Generic;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     Weight vector over <see cref="ItemParam" />. A weight of 0 excludes the
///     parameter from scoring, negative weights penalize it (e.g. Weight).
/// </summary>
public sealed class ParamWeights
{
    private readonly float[] _values = new float[ItemParams.Count];

    public float this[ItemParam param]
    {
        get => _values[(int)param];
        set => _values[(int)param] = Clamp(value);
    }

    public bool IsEmpty
    {
        get
        {
            for (var i = 0; i < _values.Length; i++)
                if (_values[i] != 0f)
                    return false;
            return true;
        }
    }

    internal float[] Raw => _values;

    public ParamWeights Clone()
    {
        var clone = new ParamWeights();
        Array.Copy(_values, clone._values, _values.Length);
        return clone;
    }

    public Dictionary<string, float> ToDictionary()
    {
        var result = new Dictionary<string, float>();
        for (var i = 0; i < _values.Length; i++)
            if (_values[i] != 0f)
                result[((ItemParam)i).ToString()] = _values[i];
        return result;
    }

    public static ParamWeights FromDictionary(IReadOnlyDictionary<string, float>? source)
    {
        var weights = new ParamWeights();
        if (source is null) return weights;

        foreach (var pair in source)
            if (Enum.TryParse(pair.Key, true, out ItemParam param))
                weights[param] = pair.Value;

        return weights;
    }

    private static float Clamp(float value) => value < -1f ? -1f : value > 1f ? 1f : value;
}
