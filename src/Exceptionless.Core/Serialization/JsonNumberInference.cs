using System.Buffers;
using System.Text.Json;

namespace Exceptionless.Core.Serialization;

internal static class JsonNumberInference
{
    public static object Read(ref Utf8JsonReader reader, bool preferInt64)
    {
        ReadOnlySpan<byte> rawValue = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;

        if (HasDecimalOrExponent(rawValue))
        {
            if (preferInt64)
                return reader.GetDouble();

            if (reader.TryGetDecimal(out decimal d))
            {
                if (ShouldPreserveDecimalZero(rawValue, d))
                    return d;

                return reader.GetDouble();
            }

            return reader.GetDouble();
        }

        if (preferInt64)
        {
            if (reader.TryGetInt64(out long l))
                return l;

            return reader.GetDouble();
        }

        if (reader.TryGetInt32(out int i))
            return i;

        if (reader.TryGetInt64(out long longValue))
            return longValue;

        if (reader.TryGetDecimal(out decimal decimalValue))
            return decimalValue;

        return reader.GetDouble();
    }

    public static object Convert(JsonElement element)
    {
        string rawText = element.GetRawText();
        if (HasDecimalOrExponent(rawText.AsSpan()))
        {
            if (element.TryGetDecimal(out decimal d))
            {
                if (ShouldPreserveDecimalZero(rawText.AsSpan(), d))
                    return d;

                return element.GetDouble();
            }

            return element.GetDouble();
        }

        if (element.TryGetInt32(out int i))
            return i;

        if (element.TryGetInt64(out long l))
            return l;

        if (element.TryGetDecimal(out decimal dec))
            return dec;

        return element.GetDouble();
    }

    private static bool HasDecimalOrExponent(ReadOnlySpan<byte> rawValue)
    {
        return rawValue.Contains((byte)'.') || rawValue.Contains((byte)'e') || rawValue.Contains((byte)'E');
    }

    private static bool HasDecimalOrExponent(ReadOnlySpan<char> rawValue)
    {
        return rawValue.Contains('.') || rawValue.Contains('e') || rawValue.Contains('E');
    }

    private static bool ShouldPreserveDecimalZero(ReadOnlySpan<byte> rawValue, decimal value)
    {
        if (value != 0m)
            return true;

        return RepresentsZero(rawValue) && !IsNegative(rawValue);
    }

    private static bool ShouldPreserveDecimalZero(ReadOnlySpan<char> rawValue, decimal value)
    {
        if (value != 0m)
            return true;

        return RepresentsZero(rawValue) && !IsNegative(rawValue);
    }

    private static bool IsNegative(ReadOnlySpan<byte> rawValue)
    {
        return rawValue.Length > 0 && rawValue[0] == (byte)'-';
    }

    private static bool IsNegative(ReadOnlySpan<char> rawValue)
    {
        return rawValue.Length > 0 && rawValue[0] == '-';
    }

    private static bool RepresentsZero(ReadOnlySpan<byte> rawValue)
    {
        foreach (byte c in rawValue)
        {
            if (c is (byte)'e' or (byte)'E')
                break;

            if (c is >= (byte)'1' and <= (byte)'9')
                return false;
        }

        return true;
    }

    private static bool RepresentsZero(ReadOnlySpan<char> rawValue)
    {
        foreach (char c in rawValue)
        {
            if (c is 'e' or 'E')
                break;

            if (c is >= '1' and <= '9')
                return false;
        }

        return true;
    }
}
