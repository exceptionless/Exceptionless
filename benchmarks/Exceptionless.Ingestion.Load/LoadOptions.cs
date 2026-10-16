namespace Exceptionless.Ingestion.Load;

internal enum EventRoute
{
    /// <summary>POST /api/v2/events: the project is resolved from the submission token.</summary>
    Events,

    /// <summary>POST /api/v2/projects/{project-id}/events: the project is explicit in the route.</summary>
    Project
}

internal enum LoadEventType
{
    Log,
    Error
}

internal enum StackScenario
{
    Hot,
    New
}

internal sealed record LoadOptions(
    Uri BaseUrl,
    string ProjectId,
    string SubmissionToken,
    string? ReadToken,
    string? ReadUser,
    string? ReadPassword,
    EventRoute Route,
    LoadEventType EventType,
    StackScenario StackScenario,
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
    string? ResultsPath,
    string? EnvironmentLabel,
    string Message,
    TimeSpan Timeout,
    TimeSpan PollInterval)
{
    // The event pipeline truncates longer messages, which would make the stored events differ from the payload sent.
    private const int MaximumMessageLength = 2000;

    private static readonly HashSet<string> _knownOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "base-url", "project-id", "submission-token", "read-token", "read-user", "read-password", "route",
        "event-type", "stack-scenario", "events", "expected-persisted", "concurrency", "batch-size", "trials",
        "warmup-events", "signature-cardinality", "discard-percent", "message-bytes", "timeout-seconds",
        "poll-interval-ms", "compression", "seed", "results", "environment-label"
    };

    public static LoadOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Every option must use '--name value'.");

            string name = args[index][2..];
            if (!_knownOptions.Contains(name))
                throw new ArgumentException($"Unknown option --{name}.");

            values[name] = args[index + 1];
        }

        string? rawBaseUrl = values.GetValueOrDefault("base-url");
        if (String.IsNullOrWhiteSpace(rawBaseUrl) || !Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme is not ("http" or "https"))
            throw new ArgumentException("--base-url must be an absolute http or https API origin.");
        if (!String.IsNullOrEmpty(baseUrl.UserInfo) || !String.IsNullOrEmpty(baseUrl.Query))
            throw new ArgumentException("--base-url must not contain credentials or a query string; use the token options instead.");

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
        EventRoute route = values.GetValueOrDefault("route", "events").ToLowerInvariant() switch
        {
            "events" => EventRoute.Events,
            "project" => EventRoute.Project,
            _ => throw new ArgumentException("--route must be events or project.")
        };
        LoadEventType eventType = values.GetValueOrDefault("event-type", "error").ToLowerInvariant() switch
        {
            "log" => LoadEventType.Log,
            "error" => LoadEventType.Error,
            _ => throw new ArgumentException("--event-type must be log or error.")
        };
        StackScenario stackScenario = values.GetValueOrDefault("stack-scenario", "hot").ToLowerInvariant() switch
        {
            "hot" => StackScenario.Hot,
            "new" => StackScenario.New,
            _ => throw new ArgumentException("--stack-scenario must be hot or new.")
        };
        int eventCount = GetInt(values, "events", 10_000, 1, 10_000_000);
        int concurrency = GetInt(values, "concurrency", 4, 1, 1024);
        int batchSize = GetInt(values, "batch-size", 100, 1, 10_000);
        int trials = GetInt(values, "trials", 3, 1, 100);
        int warmupEvents = GetInt(values, "warmup-events", 100, 0, 100_000);
        int cardinality = GetInt(values, "signature-cardinality", 10, 1, 1_000_000);
        int discardPercent = GetInt(values, "discard-percent", 0, 0, 100);
        int expectedPersisted = GetInt(values, "expected-persisted", eventCount - GetDiscardCandidateCount(eventCount, discardPercent), 0, eventCount);
        if (expectedPersisted > 0 && String.IsNullOrWhiteSpace(readToken) && (String.IsNullOrWhiteSpace(readUser) || String.IsNullOrWhiteSpace(readPassword)))
            throw new ArgumentException("Set --read-token, or both --read-user and --read-password, so query visibility can be measured.");
        if (discardPercent > 0 && eventType is not LoadEventType.Error)
            throw new ArgumentException("--discard-percent requires --event-type error.");
        if (discardPercent > 0 && stackScenario is StackScenario.New)
            throw new ArgumentException("Discard scenarios require --stack-scenario hot so the pre-discarded stack identities remain stable.");
        int messageBytes = GetInt(values, "message-bytes", 64, 0, MaximumMessageLength);
        int timeoutSeconds = GetInt(values, "timeout-seconds", 300, 1, 86_400);
        int pollIntervalMilliseconds = GetInt(values, "poll-interval-ms", 250, 10, 60_000);
        string compression = values.GetValueOrDefault("compression", "none").ToLowerInvariant();
        if (compression is not ("none" or "gzip"))
            throw new ArgumentException("--compression must be none or gzip.");

        return new LoadOptions(
            EnsureTrailingSlash(baseUrl),
            projectId,
            submissionToken,
            readToken,
            readUser,
            readPassword,
            route,
            eventType,
            stackScenario,
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
            values.GetValueOrDefault("results"),
            values.GetValueOrDefault("environment-label"),
            new string('x', messageBytes),
            TimeSpan.FromSeconds(timeoutSeconds),
            TimeSpan.FromMilliseconds(pollIntervalMilliseconds));
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("""
            Submits generated events to the V2 event API and records request latency, submission throughput,
            and the time until the persisted events are visible through the project event count query.

            dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- --base-url <origin> --project-id <id> [options]

            Connection:
              --base-url <origin>            API origin from the AppHost endpoint (aspire describe); required.
              --project-id <id>              Project that owns the events and is queried for visibility; or EXCEPTIONLESS_PROJECT_ID.
              --submission-token <key>       Project API key used to post events; or EXCEPTIONLESS_API_KEY.
              --read-token <token>           Token that can read events; or EXCEPTIONLESS_READ_TOKEN.
              --read-user <email>            Alternative to --read-token with --read-password; or EXCEPTIONLESS_READ_USER.
              --read-password <password>     Or EXCEPTIONLESS_READ_PASSWORD.
              --route events|project         events: POST /api/v2/events (default); project: POST /api/v2/projects/{id}/events.

            Scenario:
              --events <n>                   Total events per trial (default 10000).
              --batch-size <n>               Events per request (default 100). 1 posts one JSON object; larger values post a JSON array.
              --concurrency <n>              Concurrent requests (default 4).
              --compression none|gzip        Request Content-Encoding (default none).
              --trials <n>                   Measured trials (default 3).
              --warmup-events <n>            Events posted and awaited before measuring; 0 disables warmup (default 100).
              --event-type log|error         Event shape (default error).
              --stack-scenario hot|new       Reuse stack signatures across trials, or create new ones per trial (default hot).
              --signature-cardinality <n>    Distinct stack signatures per run (default 10).
              --discard-percent <0-100>      Share of events posted with stack signatures the run expects to be discarded (default 0).
              --expected-persisted <n>       Persisted events to wait for; derived from --discard-percent by default.
              --message-bytes <n>            Event message length (default 64, maximum 2000).
              --seed <text>                  Names the run's signatures and tags (default "default").

            Measurement and output:
              --poll-interval-ms <n>         Count query polling interval (default 250).
              --timeout-seconds <n>          Per-run timeout (default 300).
              --results <path>               Write a secret-free JSON evidence file.
              --environment-label <text>     Free-text topology note stored in the evidence file.
            """);
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

    private static int GetDiscardCandidateCount(int eventCount, int discardPercent)
    {
        return eventCount / 100 * discardPercent + Math.Min(eventCount % 100, discardPercent);
    }
}
