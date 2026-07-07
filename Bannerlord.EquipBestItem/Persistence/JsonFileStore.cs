using System;
using System.IO;
using Newtonsoft.Json;

namespace Bannerlord.EquipBestItem.Persistence;

/// <summary>Loads and saves POCOs as JSON files under the mod's config directory.</summary>
public sealed class JsonFileStore
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly string _directory;

    public JsonFileStore(string directory)
    {
        _directory = directory;
    }

    public T Load<T>(string fileName, Func<T> createDefault)
    {
        try
        {
            var path = Path.Combine(_directory, fileName);
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), SerializerSettings)
                       ?? createDefault();
        }
        catch (Exception exception)
        {
            GameLog.Warn($"Failed to load {fileName}: {exception.Message}");
        }

        return createDefault();
    }

    public void Save<T>(string fileName, T value)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                Path.Combine(_directory, fileName),
                JsonConvert.SerializeObject(value, SerializerSettings));
        }
        catch (Exception exception)
        {
            GameLog.Warn($"Failed to save {fileName}: {exception.Message}");
        }
    }
}
