using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Exceptionless.Core.Services;

public interface IStackFingerprintService
{
    StackFingerprint Create(EventIngestionV3Event source, Organization organization, Project project);
}

public sealed class StackFingerprintService(StackTraceParser stackTraceParser) : IStackFingerprintService
{
    private static readonly string[] _defaultNonUserNamespaces = ["System", "Microsoft"];
    private static readonly string[] _defaultCommonMethods = ["DataContext.SubmitChanges", "Entities.SaveChanges"];

    public StackFingerprint Create(EventIngestionV3Event source, Organization organization, Project project)
    {
        if (source.Stacking?.SignatureData is { Count: > 0 } manualSignature)
        {
            var data = manualSignature
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return new StackFingerprint(GetManualSignatureHash(data), data, source.Stacking.Title);
        }

        if (!String.Equals(source.Type, Event.KnownTypes.Error, StringComparison.OrdinalIgnoreCase))
        {
            var data = new Dictionary<string, string>(2);
            AddIfNotEmpty(data, "Type", source.Type);
            AddIfNotEmpty(data, "Source", source.Source);
            return new StackFingerprint(data.Values.ToSHA1(), data);
        }

        var signature = new Dictionary<string, string>(2);
        string? exceptionType = source.ExceptionType;
        string? method = null;
        string? fallbackTraceHash = null;

        if (!String.IsNullOrWhiteSpace(source.StackTrace))
        {
            string[] userNamespaces = GetSetting(organization, project, "UserNamespaces")?.SplitAndTrim([',']) ?? [];
            string[] commonMethods = GetSetting(organization, project, "CommonMethods")?.SplitAndTrim([',']) ?? _defaultCommonMethods;

            stackTraceParser.TryFindFrame(
                source.StackTrace,
                frame => IsUserFrame(frame, userNamespaces, commonMethods),
                out var firstFrame,
                out var userFrame,
                out string? selectedExceptionType);

            exceptionType = selectedExceptionType ?? exceptionType;

            var target = userFrame ?? firstFrame;
            if (target is not null)
                method = target.GetSignature();
            else
            {
                fallbackTraceHash = NormalizeFallback(source.StackTrace).ToSHA1();
                AppDiagnostics.IngestionV3ParserFallbacks.Add(1);
            }
        }

        AddIfNotEmpty(signature, "ExceptionType", exceptionType);
        AddIfNotEmpty(signature, "Method", method);
        AddIfNotEmpty(signature, "StackTrace", fallbackTraceHash);

        if (signature.Count == 0)
            AddIfNotEmpty(signature, "Type", source.Type);

        return new StackFingerprint(signature.Values.ToSHA1(), signature);
    }

    private static string? GetSetting(Organization organization, Project project, string key)
    {
        return project.Data?.GetString(key) ?? organization.Data?.GetString(key);
    }

    private static string NormalizeFallback(string stackTrace)
    {
        var normalized = new StringBuilder(stackTrace.Length);
        bool inDigits = false;
        bool inWhitespace = false;
        foreach (char value in stackTrace)
        {
            if (Char.IsDigit(value))
            {
                if (!inDigits)
                    normalized.Append('#');
                inDigits = true;
                inWhitespace = false;
                continue;
            }

            inDigits = false;
            if (Char.IsWhiteSpace(value))
            {
                if (!inWhitespace && normalized.Length > 0)
                    normalized.Append(' ');
                inWhitespace = true;
                continue;
            }

            inWhitespace = false;
            normalized.Append(value);
        }

        return normalized.ToString().Trim();
    }

    private static string GetManualSignatureHash(IReadOnlyDictionary<string, string> signature)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        AppendHashValue(hash, "exceptionless-v3-manual-stack-v1");
        foreach (var pair in signature)
        {
            AppendHashValue(hash, pair.Key);
            AppendHashValue(hash, pair.Value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendHashValue(IncrementalHash hash, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, byteCount);
        hash.AppendData(length);

        byte[]? rented = null;
        Span<byte> bytes = byteCount <= 256
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = Encoding.UTF8.GetBytes(value, bytes);
            hash.AppendData(bytes[..written]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool IsUserFrame(StackFrame frame, string[] userNamespaces, string[] commonMethods)
    {
        if (String.IsNullOrEmpty(frame.Name))
            return false;

        string? frameNamespace = frame.DeclaringNamespace;
        bool isUserNamespace = String.IsNullOrEmpty(frameNamespace)
            || (userNamespaces.Length == 0
                ? !_defaultNonUserNamespaces.Any(frameNamespace.StartsWith)
                : userNamespaces.Any(frameNamespace.StartsWith));

        return isUserNamespace && !commonMethods.Any(frame.GetSignature().Contains);
    }

    private static void AddIfNotEmpty(IDictionary<string, string> values, string key, string? value)
    {
        if (!String.IsNullOrWhiteSpace(value))
            values[key] = value;
    }
}
