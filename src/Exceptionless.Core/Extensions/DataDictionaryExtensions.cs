using Exceptionless.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    public static T? GetValue<T>(this DataDictionary extendedData, string key)
    {
        if (!extendedData.TryGetValue(key, out object? data))
            throw new KeyNotFoundException($"Key \"{key}\" not found in the dictionary.");

        if (data is T value)
            return value;

        if (data is JObject jObject)
        {
            try
            {
                return jObject.ToObject<T>();
            }
            catch { }
        }

        if (data is string json && json.IsJson())
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch { }
        }

        try
        {
            if (data != null)
            {
                return data.ToType<T>();
            }
        }
        catch { }

        return default;
    }

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = extendedData.Keys.Where(k => k.StartsWith("-")).ToArray();
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
