namespace Exceptionless.Core.Models.Ingestion;

public sealed record StackFingerprint(string SignatureHash, IReadOnlyDictionary<string, string> SignatureData, string? Title = null);
