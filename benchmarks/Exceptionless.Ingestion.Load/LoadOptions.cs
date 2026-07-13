namespace Exceptionless.Ingestion.Load;

internal enum IngestionProtocol
{
    V2,
    V3
}

internal enum LoadEventType
{
    Log,
    Error
}

internal sealed record LoadOptions(
    Uri BaseUrl,
    string ProjectId,
    string SubmissionToken,
    string? ReadToken,
    string? ReadUser,
    string? ReadPassword,
    IReadOnlyList<IngestionProtocol> Protocols,
    LoadEventType EventType,
    int EventCount,
    int ExpectedPersisted,
    int Concurrency,
    int BatchSize,
    int Trials,
    int WarmupEvents,
    int SignatureCardinality,
    int DiscardPercent,
    string Compression,
    string Seed,
    string Message,
    TimeSpan Timeout,
    TimeSpan PollInterval)
{
    public static LoadOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Every option must use '--name value'.");
            values[args[index][2..]] = args[index + 1];
        }

        string? rawBaseUrl = values.GetValueOrDefault("base-url");
        if (String.IsNullOrWhiteSpace(rawBaseUrl) || !Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var baseUrl))
            throw new ArgumentException("--base-url must be an absolute API origin.");

        string projectId = values.GetValueOrDefault("project-id")
            ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_PROJECT_ID")
            ?? throw new ArgumentException("Set --project-id or EXCEPTIONLESS_PROJECT_ID.");
        string submissionToken = values.GetValueOrDefault("submission-token")
            ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_API_KEY")
            ?? throw new ArgumentException("Set --submission-token or EXCEPTIONLESS_API_KEY.");
        string? readToken = values.GetValueOrDefault("read-token")
            ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_READ_TOKEN");
        string? readUser = values.GetValueOrDefault("read-user")
            ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_READ_USER");
        string? readPassword = values.GetValueOrDefault("read-password")
            ?? Environment.GetEnvironmentVariable("EXCEPTIONLESS_READ_PASSWORD");
        IReadOnlyList<IngestionProtocol> protocols = ParseProtocols(values.GetValueOrDefault("protocol", "both"));
        if (protocols.Contains(IngestionProtocol.V2) && String.IsNullOrWhiteSpace(readToken) && (String.IsNullOrWhiteSpace(readUser) || String.IsNullOrWhiteSpace(readPassword)))
            throw new ArgumentException("Set --read-token, or both --read-user and --read-password, so V2 processing completion can be measured.");

        LoadEventType eventType = values.GetValueOrDefault("event-type", "error").ToLowerInvariant() switch
        {
            "log" => LoadEventType.Log,
            "error" => LoadEventType.Error,
            _ => throw new ArgumentException("--event-type must be log or error.")
        };
        int eventCount = GetInt(values, "events", 10_000, 1, 10_000_000);
        int expectedPersisted = GetInt(values, "expected-persisted", eventCount, 0, eventCount);
        int concurrency = GetInt(values, "concurrency", 4, 1, 1024);
        int batchSize = GetInt(values, "batch-size", 100, 1, 10_000);
        int trials = GetInt(values, "trials", 3, 1, 100);
        int warmupEvents = GetInt(values, "warmup-events", 100, 0, 100_000);
        int cardinality = GetInt(values, "signature-cardinality", 10, 1, 1_000_000);
        int discardPercent = GetInt(values, "discard-percent", 0, 0, 100);
        int messageBytes = GetInt(values, "message-bytes", 64, 0, 4000);
        int timeoutSeconds = GetInt(values, "timeout-seconds", 300, 1, 86_400);
        int pollIntervalMilliseconds = GetInt(values, "poll-interval-ms", 250, 10, 60_000);
        string compression = values.GetValueOrDefault("compression", "none").ToLowerInvariant();
        if (compression is not ("none" or "gzip"))
            throw new ArgumentException("Apples-to-apples comparisons support the encodings common to both APIs: none or gzip.");

        return new LoadOptions(
            EnsureTrailingSlash(baseUrl),
            projectId,
            submissionToken,
            readToken,
            readUser,
            readPassword,
            protocols,
            eventType,
            eventCount,
            expectedPersisted,
            concurrency,
            batchSize,
            trials,
            warmupEvents,
            cardinality,
            discardPercent,
            compression,
            SanitizeSeed(values.GetValueOrDefault("seed", "default")),
            new string('x', messageBytes),
            TimeSpan.FromSeconds(timeoutSeconds),
            TimeSpan.FromMilliseconds(pollIntervalMilliseconds));
    }

    public static void WriteUsage()
    {
        Console.Error.WriteLine("dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- --base-url <origin> --project-id <id> --protocol v2|v3|both [--submission-token <key>] [--read-token <token> | --read-user <email> --read-password <password>] [--events 10000] [--batch-size 1|1000] [--concurrency 4] [--trials 3] [--warmup-events 100] [--event-type log|error] [--signature-cardinality 10] [--compression none|gzip]");
    }

    private static IReadOnlyList<IngestionProtocol> ParseProtocols(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "v2" => [IngestionProtocol.V2],
            "v3" => [IngestionProtocol.V3],
            "both" => [IngestionProtocol.V2, IngestionProtocol.V3],
            _ => throw new ArgumentException("--protocol must be v2, v3, or both.")
        };
    }

    private static Uri EnsureTrailingSlash(Uri value)
    {
        var builder = new UriBuilder(value);
        if (!builder.Path.EndsWith('/'))
            builder.Path += "/";
        return builder.Uri;
    }

    private static string SanitizeSeed(string value)
    {
        string sanitized = new(value.Where(c => Char.IsAsciiLetterOrDigit(c) || c == '-').Take(24).ToArray());
        return String.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue, int minimum, int maximum)
    {
        if (!values.TryGetValue(key, out string? raw))
            return defaultValue;
        if (!Int32.TryParse(raw, out int value) || value < minimum || value > maximum)
            throw new ArgumentException($"--{key} must be between {minimum} and {maximum}.");
        return value;
    }
}
