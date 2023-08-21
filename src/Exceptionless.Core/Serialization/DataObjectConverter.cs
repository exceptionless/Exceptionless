using System.Collections.Concurrent;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Reflection;
using Foundatio.Repositories.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Serializer;

public class DataObjectConverter<T> : CustomCreationConverter<T> where T : IData, new()
{
    private static readonly Type _type = typeof(T);
    private static readonly ConcurrentDictionary<string, IMemberAccessor> _propertyAccessors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Type> _dataTypeRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;
    private readonly char[] _filteredChars = { '.', '-', '_' };

    public DataObjectConverter(ILogger logger, IEnumerable<KeyValuePair<string, Type>>? knownDataTypes = null)
    {
        _logger = logger;

        if (knownDataTypes is not null)
            _dataTypeRegistry.AddRange(knownDataTypes);

        if (_propertyAccessors.Count != 0)
            return;

        foreach (var prop in _type.GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public).Where(p => p.CanWrite))
            _propertyAccessors.TryAdd(prop.Name, LateBinder.GetPropertyAccessor(prop));
    }

    public void AddKnownDataType(string name, Type dataType)
    {
        _dataTypeRegistry.TryAdd(name, dataType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var target = Create(objectType);
        var json = JObject.Load(reader);

        foreach (var p in json.Properties())
        {
            string propertyName = p.Name.ToLowerFiltered(_filteredChars);

            if (propertyName == "data" && p.Value is JObject)
            {
                foreach (var dataProp in ((JObject)p.Value).Properties())
                    AddDataEntry(serializer, dataProp, target);

                continue;
            }

            var accessor = _propertyAccessors.TryGetValue(propertyName, out var value) ? value : null;
            if (accessor is not null)
            {
                if (p.Value.Type == JTokenType.None || p.Value.Type == JTokenType.Undefined)
                    continue;

                if (p.Value.Type == JTokenType.Null)
                {
                    accessor.SetValue(target, null);
                    continue;
                }

                if (accessor.MemberType == typeof(DateTime))
                {
                    if (p.Value.Type == JTokenType.Date || p.Value.Type == JTokenType.String && p.Value.Value<string>()!.Contains("+"))
                    {
                        accessor.SetValue(target, p.Value.ToObject<DateTimeOffset>(serializer).DateTime);
                        continue;
                    }
                }
                else if (accessor.MemberType == typeof(DateTime?))
                {
                    if (p.Value.Type == JTokenType.Date || p.Value.Type == JTokenType.String && p.Value.Value<string>()!.Contains("+"))
                    {
                        var offset = p.Value.ToObject<DateTimeOffset?>(serializer);
                        accessor.SetValue(target, offset?.DateTime);
                        continue;
                    }
                }

                accessor.SetValue(target, p.Value.ToObject(accessor.MemberType, serializer));
                continue;
            }

            AddDataEntry(serializer, p, target);
        }

        return target;
    }

    private void AddDataEntry(JsonSerializer serializer, JProperty p, T target)
    {
        if (target.Data is null)
            target.Data = new DataDictionary();

        string dataKey = GetDataKey(target.Data, p.Name);
        string unknownTypeDataKey = GetDataKey(target.Data, p.Name, true);

        // when adding items to data, see if they are a known type and deserialize to the registered type
        if (_dataTypeRegistry.TryGetValue(p.Name, out var dataType))
        {
            try
            {
                if (p.Value is JValue && p.Value.Type == JTokenType.String)
                {
                    string value = p.Value.ToString();
                    if (value.IsJson())
                        target.Data[dataKey] = serializer.Deserialize(new StringReader(value), dataType);
                    else
                        target.Data[dataType == value.GetType() ? dataKey : unknownTypeDataKey] = value;
                }
                else
                {
                    target.Data[dataKey] = p.Value.ToObject(dataType, serializer);
                }

                return;
            }
            catch (Exception)
            {
                _logger.LogInformation("Error deserializing known data type {Name}: {Value}", p.Name, p.Value.ToString());
            }
        }

        // Add item to data as a JObject, JArray or native type.
        if (p.Value is JObject)
        {
            target.Data[dataType is null || dataType == typeof(JObject) ? dataKey : unknownTypeDataKey] = p.Value.ToObject<JObject>();
        }
        else if (p.Value is JArray)
        {
            target.Data[dataType is null || dataType == typeof(JArray) ? dataKey : unknownTypeDataKey] = p.Value.ToObject<JArray>();
        }
        else if (p.Value is JValue jValue && jValue.Type != JTokenType.String)
        {
            object? value = jValue.Value;
            target.Data[dataType is null || dataType == value?.GetType() ? dataKey : unknownTypeDataKey] = value;
        }
        else
        {
            string value = p.Value.ToString();
            var jsonType = value.GetJsonType();
            if (jsonType == JsonType.Object)
            {
                if (value.TryFromJson(out JObject? obj))
                    target.Data[dataType is null || dataType == obj?.GetType() ? dataKey : unknownTypeDataKey] = obj;
                else
                    target.Data[dataType is null || dataType == value.GetType() ? dataKey : unknownTypeDataKey] = value;
            }
            else if (jsonType == JsonType.Array)
            {
                if (value.TryFromJson(out JArray? obj))
                    target.Data[dataType is null || dataType == obj?.GetType() ? dataKey : unknownTypeDataKey] = obj;
                else
                    target.Data[dataType is null || dataType == value.GetType() ? dataKey : unknownTypeDataKey] = value;
            }
            else
            {
                target.Data[dataType is null || dataType == value.GetType() ? dataKey : unknownTypeDataKey] = value;
            }
        }
    }

    private string GetDataKey(DataDictionary data, string dataKey, bool isUnknownType = false)
    {
        if (data.ContainsKey(dataKey) || isUnknownType)
            dataKey = dataKey.StartsWith("@") ? "_" + dataKey : dataKey;

        int count = 1;
        string key = dataKey;
        while (data.ContainsKey(key) || (isUnknownType && _dataTypeRegistry.ContainsKey(key)))
            key = dataKey + count++;

        return key;
    }

    public override T Create(Type objectType)
    {
        return new T();
    }

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
    {
        return objectType == _type;
    }
}
