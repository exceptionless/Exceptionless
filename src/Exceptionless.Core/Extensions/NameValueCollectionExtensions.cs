using System.Collections.Specialized;

namespace Exceptionless.Core.Extensions;

public static class NameValueCollectionExtensions
{
    public static string? GetValue(this NameValueCollection collection, string name, string? defaultValue = null)
    {
        return collection[name] ?? defaultValue;
    }

    public static int? GetInt(this NameValueCollection collection, string name)
    {
        string? value = collection[name];
        if (value is null)
            return null;

        if (Int32.TryParse(value, out int number))
            return number;

        return null;
    }

    public static int GetInt(this NameValueCollection collection, string name, int defaultValue)
    {
        return GetInt(collection, name) ?? defaultValue;
    }

    public static long GetInt64(this NameValueCollection collection, string name, long defaultValue)
    {
        return GetInt64(collection, name) ?? defaultValue;
    }

    public static long? GetInt64(this NameValueCollection collection, string name)
    {
        string? value = collection[name];
        if (value is null)
            return null;

        if (Int64.TryParse(value, out long number))
            return number;

        return null;
    }

    public static bool? GetBool(this NameValueCollection collection, string name)
    {
        string? value = collection[name];
        if (value is null)
            return null;

        if (Boolean.TryParse(value, out bool boolean))
            return boolean;

        return null;
    }

    public static bool GetBool(this NameValueCollection collection, string name, bool defaultValue)
    {
        return GetBool(collection, name) ?? defaultValue;
    }

    public static T GetEnum<T>(this NameValueCollection collection, string name, T? defaultValue = null) where T : struct
    {
        string? value = GetValue(collection, name);
        if (value is null)
        {
            if (defaultValue.HasValue && defaultValue is T)
                return (T)defaultValue;

            throw new Exception($"The configuration key '{name}' was not found and no default value was specified.");
        }

        try
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
        catch (ArgumentException ex)
        {
            if (defaultValue.HasValue && defaultValue is T)
                return (T)defaultValue;

            string message = $"Configuration key '{name}' has value '{value}' that could not be parsed as a member of the {typeof(T).Name} enum type.";
            throw new Exception(message, ex);
        }
    }

    public static List<string>? GetStringList(this NameValueCollection collection, string name, string? defaultValues = null, char[]? separators = null)
    {
        string? value = collection[name];
        if (value is null && defaultValues is null)
            return null;

        if (value is null)
            value = defaultValues;

        if (separators is null)
            separators = [','];

        return value?
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }
}
