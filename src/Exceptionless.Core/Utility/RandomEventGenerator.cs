using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Core.Utility;

public class RandomEventGenerator
{
    private readonly TimeProvider _timeProvider;

    public RandomEventGenerator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public List<PersistentEvent> Generate(string organizationId, string projectId, int count, DateTime? minDate = null, DateTime? maxDate = null)
    {
        var events = new List<PersistentEvent>(count);
        var min = minDate ?? _timeProvider.GetUtcNow().UtcDateTime.AddDays(-7);
        var max = maxDate ?? _timeProvider.GetUtcNow().UtcDateTime;

        for (int i = 0; i < count; i++)
        {
            var ev = new PersistentEvent
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Date = RandomData.GetDateTime(min, max)
            };

            PopulateEvent(ev);
            events.Add(ev);
        }

        return events;
    }

    private void PopulateEvent(Event ev)
    {
        ev.Data ??= new DataDictionary();
        ev.Tags ??= [];

        ev.Type = EventTypes.Random()!;
        switch (ev.Type)
        {
            case Event.KnownTypes.FeatureUsage:
                ev.Source = FeatureNames.Random();
                break;
            case Event.KnownTypes.NotFound:
                ev.Source = PageNames.Random();
                break;
            case Event.KnownTypes.Log:
                ev.Source = LogSources.Random();
                ev.Message = LogMessages.Random();
                string? level = LogLevels.Random();
                if (!String.IsNullOrEmpty(level))
                    ev.Data[Event.KnownDataKeys.Level] = level;
                break;
        }

        if (RandomData.GetBool(70))
            ev.Geo = RandomData.GetCoordinate();

        if (RandomData.GetBool(30))
            ev.Value = RandomData.GetInt(0, 10000);

        string? identity = Identities.Random();
        if (!String.IsNullOrEmpty(identity))
            ev.SetUserIdentity(identity);

        ev.SetVersion(RandomData.GetVersion("2.0", "4.0"));

        ev.AddRequestInfo(new RequestInfo
        {
            Path = PageNames.Random()
        });

        ev.Data[Event.KnownDataKeys.EnvironmentInfo] = new EnvironmentInfo
        {
            IpAddress = MachineIpAddresses.Random() + ", " + MachineIpAddresses.Random(),
            MachineName = MachineNames.Random()
        };

        for (int i = 0; i < RandomData.GetInt(1, 3); i++)
        {
            string key = RandomData.GetWord();
            while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                key = RandomData.GetWord();
            ev.Data.Add(key, RandomData.GetString());
        }

        int tagCount = RandomData.GetInt(1, 3);
        for (int i = 0; i < tagCount; i++)
        {
            string? tag = EventTags.Random();
            if (tag is not null)
                ev.Tags.Add(tag);
        }

        if (ev.Type == Event.KnownTypes.Error)
        {
            // Pre-generate a limited set of errors so stacking occurs
            _randomErrors ??= [.. Enumerable.Range(1, 15).Select(_ => GenerateError())];
            _randomSimpleErrors ??= [.. Enumerable.Range(1, 10).Select(_ => GenerateSimpleError())];

            if (RandomData.GetBool())
                ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
            else
                ev.Data[Event.KnownDataKeys.SimpleError] = _randomSimpleErrors.Random();
        }
    }

    private List<Error>? _randomErrors;
    private List<SimpleError>? _randomSimpleErrors;

    private Error GenerateError(int maxNesting = 3, int currentLevel = 0)
    {
        var error = new Error
        {
            Message = ErrorMessages.Random()!,
            Type = ExceptionTypes.Random()
        };

        if (RandomData.GetBool())
            error.Code = RandomData.GetInt(-234523453, 98690899).ToString();

        error.Data = new DataDictionary();
        for (int i = 0; i < RandomData.GetInt(1, 3); i++)
        {
            string key = RandomData.GetWord();
            while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                key = RandomData.GetWord();
            error.Data.Add(key, RandomData.GetString());
        }

        var stack = new StackFrameCollection();
        for (int i = 0; i < RandomData.GetInt(2, 8); i++)
            stack.Add(GenerateStackFrame());
        error.StackTrace = stack;

        if (currentLevel < maxNesting && RandomData.GetBool())
            error.Inner = GenerateError(maxNesting, currentLevel + 1);

        return error;
    }

    private SimpleError GenerateSimpleError(int maxNesting = 3, int currentLevel = 0)
    {
        var error = new SimpleError
        {
            Message = ErrorMessages.Random()!,
            Type = ExceptionTypes.Random()
        };

        error.Data = new DataDictionary();
        for (int i = 0; i < RandomData.GetInt(1, 3); i++)
        {
            string key = RandomData.GetWord();
            while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                key = RandomData.GetWord();
            error.Data.Add(key, RandomData.GetString());
        }

        error.StackTrace = RandomData.GetString();

        if (currentLevel < maxNesting && RandomData.GetBool())
            error.Inner = GenerateSimpleError(maxNesting, currentLevel + 1);

        return error;
    }

    private static StackFrame GenerateStackFrame()
    {
        return new StackFrame
        {
            DeclaringNamespace = Namespaces.Random(),
            DeclaringType = TypeNames.Random(),
            Name = MethodNames.Random(),
            Parameters = [new Parameter { Type = "String", Name = "path" }]
        };
    }

    private static readonly List<string> Identities =
    [
        "eric@exceptionless.io",
        "blake@exceptionless.io",
        "support@exceptionless.io",
        "dev@exceptionless.io",
        "user42@example.com"
    ];

    private static readonly List<string> MachineIpAddresses =
    [
        "127.34.36.89", "45.66.89.98", "10.12.18.193", "16.89.17.197", "43.10.99.234"
    ];

    private static readonly List<string> LogSources =
    [
        "Exceptionless.Core.Pipeline.EventPipeline",
        "Exceptionless.Core.Services.UsageService",
        "Microsoft.AspNetCore.Hosting.WebHost",
        "Foundatio.Jobs.WorkItemJob"
    ];

    private static readonly List<string> LogMessages =
    [
        "Processing event batch completed successfully",
        "Cache miss for organization settings",
        "Retrying failed operation after transient error",
        "Request completed in 234ms",
        "Background job started",
        "Connection pool exhausted, waiting for available connection",
        "Configuration reloaded from source",
        "Health check passed"
    ];

    private static readonly List<string> LogLevels =
    [
        "Trace", "Debug", "Info", "Info", "Warn", "Error"
    ];

    private static readonly List<string> FeatureNames =
    [
        "Dashboard", "Event Search", "Stack Details", "Project Settings",
        "User Management", "Notifications", "API Usage", "Billing"
    ];

    private static readonly List<string> MachineNames =
    [
        "web-prod-01", "web-prod-02", "api-prod-01", "worker-01", "worker-02"
    ];

    private static readonly List<string> PageNames =
    [
        "/dashboard", "/project/settings", "/account/manage", "/event/search",
        "/stack/details", "/api/v2/events", "/api/v2/stacks", "/next/login"
    ];

    private static readonly List<string> EventTypes =
    [
        Event.KnownTypes.Error, Event.KnownTypes.Error, Event.KnownTypes.Error,
        Event.KnownTypes.Log, Event.KnownTypes.Log,
        Event.KnownTypes.FeatureUsage,
        Event.KnownTypes.NotFound
    ];

    private static readonly List<string> ExceptionTypes =
    [
        "System.NullReferenceException",
        "System.ArgumentException",
        "System.InvalidOperationException",
        "System.AggregateException",
        "System.IO.FileNotFoundException",
        "System.Net.Http.HttpRequestException",
        "System.TimeoutException",
        "System.UnauthorizedAccessException"
    ];

    private static readonly List<string> ErrorMessages =
    [
        "Object reference not set to an instance of an object.",
        "Value cannot be null. (Parameter 'organizationId')",
        "Sequence contains no elements",
        "The operation was canceled.",
        "Could not load file or assembly 'Newtonsoft.Json'",
        "A task was canceled.",
        "Connection refused (localhost:9200)",
        "The request was aborted: Could not create SSL/TLS secure channel.",
        "Index not found: stacks-v1-000001",
        "Timeout expired. The timeout period elapsed prior to completion of the operation."
    ];

    private static readonly List<string> EventTags =
    [
        "Critical", "Production", "UI", "API", "Background",
        "Authentication", "Search", "Billing", "Notification", "Import"
    ];

    private static readonly List<string> Namespaces =
    [
        "System", "System.IO", "System.Net.Http",
        "Exceptionless.Core.Pipeline", "Exceptionless.Core.Services",
        "Foundatio.Jobs", "Foundatio.Repositories"
    ];

    private static readonly List<string> TypeNames =
    [
        "EventPipeline", "UsageService", "StackRepository",
        "EventPostsJob", "OrganizationController", "BillingManager"
    ];

    private static readonly List<string> MethodNames =
    [
        "ProcessAsync", "RunAsync", "GetByIdAsync",
        "SaveAsync", "HandleItemAsync", "ValidateAsync"
    ];
}
