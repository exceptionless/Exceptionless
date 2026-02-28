using System.ComponentModel;
using System.Text.Json;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Utility;

public interface IExtensibleObject
{
    void SetProperty<T>(string name, T value);

    T? GetProperty<T>(string name);

    object? GetProperty(string name);

    bool HasProperty(string name);

    void RemoveProperty(string name);

    IEnumerable<KeyValuePair<string, object?>> GetProperties();
}

public class ExtensibleObject : INotifyPropertyChanged, IExtensibleObject
{
    private readonly Dictionary<string, object?> _extendedData = new();

    public void SetProperty<T>(string name, T value)
    {
        if (_extendedData.ContainsKey(name))
            _extendedData[name] = value;
        else
            _extendedData.Add(name, value);

        NotifyPropertyChanged(name);
    }

    public T? GetProperty<T>(string name)
    {
        object? value = GetProperty(name);
        if (value is null)
            throw new InvalidOperationException($"Property value \"{name}\" is null.  Can't use generic method on null values.");

        if (value is T tValue)
            return tValue;

        // Handle JsonElement from STJ deserialization
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>();
            }
            catch
            {
                // Fall through to ToType conversion
            }
        }

        return value.ToType<T>();
    }

    public object? GetProperty(string name)
    {
        return _extendedData.TryGetValue(name, out object? value) ? value : null;
    }

    public IEnumerable<KeyValuePair<string, object?>> GetProperties()
    {
        return _extendedData;
    }

    public bool HasProperty(string name)
    {
        return _extendedData.ContainsKey(name);
    }

    public void RemoveProperty(string name)
    {
        if (_extendedData.ContainsKey(name))
        {
            _extendedData.Remove(name);
            NotifyPropertyChanged(name);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
