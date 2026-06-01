using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Repositories.Utility;
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
        if (count <= 0)
            return [];

        var events = new List<PersistentEvent>(count);
        var min = minDate ?? _timeProvider.GetUtcNow().UtcDateTime.AddDays(-7);
        var max = maxDate ?? _timeProvider.GetUtcNow().UtcDateTime;

        // Reserve ~20% of events for sessions
        int sessionCount = Math.Clamp(count / 5, 0, count);
        int regularCount = count - sessionCount;
        int errorLogCount = Math.Clamp(regularCount / 10, regularCount > 0 ? 1 : 0, regularCount);

        for (int i = 0; i < regularCount; i++)
        {
            var ev = new PersistentEvent
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Date = RandomData.GetDateTime(min, max)
            };

            PopulateEvent(ev, i < errorLogCount ? "Error" : null);
            if (i % 5 == 0)
            {
                ev.ReferenceId = GenerateReferenceId();
            }

            events.Add(ev);
        }

        // Generate session events
        for (int i = 0; i < sessionCount; i++)
        {
            var sessionEvents = GenerateSession(organizationId, projectId, min, max);
            events.AddRange(sessionEvents);
        }

        return events;
    }

    private List<PersistentEvent> GenerateSession(string organizationId, string projectId, DateTime minDate, DateTime maxDate)
    {
        var events = new List<PersistentEvent>();
        string sessionId = ObjectId.GenerateNewId().ToString();
        string? identity = Identities.Random();
        var startDate = RandomData.GetDateTime(minDate, maxDate.AddHours(-1));
        int durationSeconds = RandomData.GetInt(30, 7200); // 30s to 2 hours
        var endDate = startDate.AddSeconds(durationSeconds);
        bool isActive = endDate > _timeProvider.GetUtcNow().UtcDateTime || RandomData.GetBool(15); // 15% chance of active

        // Session start event
        var sessionStart = new PersistentEvent
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Date = startDate,
            Type = Event.KnownTypes.Session,
            ReferenceId = sessionId,
            Value = isActive ? null : durationSeconds,
            Data = new DataDictionary()
        };

        if (!String.IsNullOrEmpty(identity))
            sessionStart.SetUserIdentity(identity);

        if (!isActive)
            sessionStart.Data["sessionend"] = endDate;

        sessionStart.SetVersion(RandomData.GetVersion("2.0", "4.0"));

        if (RandomData.GetBool(60))
            sessionStart.Geo = RandomData.GetCoordinate();

        events.Add(sessionStart);

        return events;
    }

    private void PopulateEvent(Event ev, string? logLevel = null)
    {
        ev.Data ??= new DataDictionary();
        ev.Tags ??= [];

        ev.Type = String.IsNullOrEmpty(logLevel) ? EventTypes.Random()! : Event.KnownTypes.Log;
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
                string? level = logLevel ?? LogLevels.Random();
                ev.Message = GetLogMessage(level);
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

        AddSampleExtendedData(ev, identity);

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
            _randomErrors ??= [.. Enumerable.Range(1, 5).Select(_ => GenerateError())];
            _randomSimpleErrors ??= [.. Enumerable.Range(1, 3).Select(_ => GenerateSimpleError())];

            if (RandomData.GetBool())
                ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
            else
                ev.Data[Event.KnownDataKeys.SimpleError] = _randomSimpleErrors.Random();
        }
    }

    private static string? GetLogMessage(string? level)
    {
        return String.Equals(level, "Error", StringComparison.OrdinalIgnoreCase)
            ? ErrorLogMessages.Random()
            : LogMessages.Random();
    }

    private static string GenerateReferenceId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 10);
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

        error.Data = GenerateErrorData();

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

        error.Data = GenerateErrorData();

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

    private static void AddSampleExtendedData(Event ev, string? identity)
    {
        var data = ev.Data ??= new DataDictionary();

        data["Customer"] = new DataDictionary
        {
            ["id"] = $"cus_{RandomData.GetInt(100000, 999999)}",
            ["email"] = identity ?? Identities.Random(),
            ["plan"] = Plans.Random(),
            ["region"] = Regions.Random(),
            ["account_age_days"] = RandomData.GetInt(7, 1600)
        };

        data["Deployment"] = new DataDictionary
        {
            ["environment"] = Environments.Random(),
            ["version"] = RandomData.GetVersion("2.0", "4.0"),
            ["commit"] = ObjectId.GenerateNewId().ToString()[..7],
            ["region"] = Regions.Random(),
            ["instance"] = MachineNames.Random()
        };

        switch (ev.Type)
        {
            case Event.KnownTypes.FeatureUsage:
                data["Feature Flags"] = new DataDictionary
                {
                    ["dashboard_refresh"] = RandomData.GetBool(),
                    ["billing_portal"] = RandomData.GetBool(),
                    ["new_events_view"] = RandomData.GetBool(),
                    ["checkout_variant"] = FeatureVariants.Random()
                };
                break;
            case Event.KnownTypes.Log:
                data["Background Job"] = new DataDictionary
                {
                    ["id"] = $"job_{RandomData.GetInt(100000, 999999)}",
                    ["name"] = JobNames.Random(),
                    ["queue"] = Queues.Random(),
                    ["attempt"] = RandomData.GetInt(1, 4),
                    ["duration_ms"] = RandomData.GetInt(25, 15000)
                };
                break;
            case Event.KnownTypes.NotFound:
                data["Route Match"] = new DataDictionary
                {
                    ["requested_path"] = ev.Source,
                    ["referer"] = PageNames.Random(),
                    ["rule"] = "legacy-route-redirect",
                    ["locale"] = Locales.Random(),
                    ["bot"] = RandomData.GetBool(20)
                };
                break;
            default:
                data["Checkout"] = new DataDictionary
                {
                    ["order_id"] = $"ord_{RandomData.GetInt(100000, 999999)}",
                    ["cart_id"] = $"cart_{RandomData.GetInt(100000, 999999)}",
                    ["total"] = RandomData.GetInt(2499, 25999) / 100m,
                    ["currency"] = "USD",
                    ["item_count"] = RandomData.GetInt(1, 8),
                    ["payment_provider"] = PaymentProviders.Random()
                };
                break;
        }
    }

    private static DataDictionary GenerateErrorData()
    {
        return new DataDictionary
        {
            ["correlation_id"] = ObjectId.GenerateNewId().ToString(),
            ["tenant"] = new DataDictionary
            {
                ["id"] = $"org_{RandomData.GetInt(10000, 99999)}",
                ["plan"] = Plans.Random(),
                ["region"] = Regions.Random()
            },
            ["operation"] = new DataDictionary
            {
                ["name"] = Operations.Random(),
                ["attempt"] = RandomData.GetInt(1, 4),
                ["elapsed_ms"] = RandomData.GetInt(20, 30000),
                ["retryable"] = RandomData.GetBool()
            }
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

    private static readonly List<string> Plans =
    [
        "Free", "Team", "Business", "Enterprise"
    ];

    private static readonly List<string> Regions =
    [
        "us-east-1", "us-west-2", "eu-west-1", "ap-southeast-2"
    ];

    private static readonly List<string> Environments =
    [
        "Production", "Staging", "Preview"
    ];

    private static readonly List<string> FeatureVariants =
    [
        "control", "variant-a", "variant-b", "staff-only"
    ];

    private static readonly List<string> PaymentProviders =
    [
        "Stripe", "Braintree", "Adyen", "PayPal"
    ];

    private static readonly List<string> JobNames =
    [
        "ProcessEventBatch", "SendNotificationEmail", "SyncBillingUsage", "RebuildStackSummary"
    ];

    private static readonly List<string> Queues =
    [
        "events", "notifications", "billing", "maintenance"
    ];

    private static readonly List<string> Locales =
    [
        "en-US", "en-GB", "fr-FR", "de-DE"
    ];

    private static readonly List<string> Operations =
    [
        "LoadProjectSettings", "SubmitCheckout", "SearchEvents", "ProcessQueueMessage"
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

    private static readonly List<string> ErrorLogMessages =
    [
        "Failed to process event batch after retry limit was reached",
        "Unhandled exception while processing background job",
        "Unable to publish notification email",
        "Database command failed while saving project usage",
        "Elasticsearch request failed while searching events"
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
