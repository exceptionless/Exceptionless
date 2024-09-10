using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Utility;

public class EventData
{
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IEventRepository _eventRepository;
    private readonly EventParserPluginManager _parserPluginManager;
    private readonly TimeProvider _timeProvider;
    private List<Error>? _randomErrors;

    public EventData(ExceptionlessElasticConfiguration configuration, IEventRepository eventRepository, EventParserPluginManager parserPluginManager, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _eventRepository = eventRepository;
        _parserPluginManager = parserPluginManager;
        _timeProvider = timeProvider;
    }

    public IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string[]? organizationIds = null, string[]? projectIds = null, string[]? stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, string[]? referenceIds = null, decimal? value = -1, string? semver = null)
    {
        for (int i = 0; i < count; i++)
            yield return GenerateEvent(organizationIds, projectIds, stackIds, startDate, endDate, generateTags: generateTags, generateData: generateData, maxErrorNestingLevel: maxErrorNestingLevel, referenceIds: referenceIds, value: value, semver: semver);
    }

    public IEnumerable<PersistentEvent> GenerateEvents(int count = 10, string? organizationId = null, string? projectId = null, string? stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, int maxErrorNestingLevel = 3, bool generateTags = true, bool generateData = true, string? referenceId = null, decimal? value = -1, string? semver = null)
    {
        for (int i = 0; i < count; i++)
            yield return GenerateEvent(organizationId, projectId, stackId, startDate, endDate, generateTags: generateTags, generateData: generateData, maxErrorNestingLevel: maxErrorNestingLevel, referenceId: referenceId, value: value, semver: semver);
    }

    public PersistentEvent GenerateSampleEvent()
    {
        return GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, maxErrorNestingLevel: 4);
    }

    public PersistentEvent GenerateEvent(string? organizationId = null, string? projectId = null, string? stackId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, string? referenceId = null, string? type = null, string? sessionId = null, string? userIdentity = null, decimal? value = -1, string? semver = null, string? source = null)
    {
        return GenerateEvent(
            organizationId is not null ? [organizationId] : null,
            projectId is not null ? [projectId] : null,
            stackId is not null ? [stackId] : null,
            startDate,
            endDate,
            occurrenceDate,
            maxErrorNestingLevel,
            generateTags,
            generateData,
            referenceId is not null ? [referenceId] : null,
            type,
            sessionId,
            userIdentity,
            value,
            semver,
            source
        );
    }

    public PersistentEvent GenerateSessionStartEvent(DateTimeOffset occurrenceDate, string? sessionId = null, string? userIdentity = null, decimal? value = -1)
    {
        return GenerateEvent(projectIds: [], type: Event.KnownTypes.Session, occurrenceDate: occurrenceDate, sessionId: sessionId, userIdentity: userIdentity, generateData: false, generateTags: false, value: value);
    }

    public PersistentEvent GenerateSessionEndEvent(DateTimeOffset occurrenceDate, string? sessionId = null, string? userIdentity = null)
    {
        return GenerateEvent(projectIds: [], type: Event.KnownTypes.SessionEnd, occurrenceDate: occurrenceDate, sessionId: sessionId, userIdentity: userIdentity, generateData: false, generateTags: false);
    }

    public PersistentEvent GenerateEvent(string[]? organizationIds = null, string[]? projectIds = null, string[]? stackIds = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, DateTimeOffset? occurrenceDate = null, int maxErrorNestingLevel = 0, bool generateTags = true, bool generateData = true, string[]? referenceIds = null, string? type = null, string? sessionId = null, string? userIdentity = null, decimal? value = -1, string? semver = null, string? source = null)
    {
        if (!startDate.HasValue || startDate > _timeProvider.GetLocalNow().AddHours(1))
            startDate = _timeProvider.GetLocalNow().AddDays(-30);
        if (!endDate.HasValue || endDate > _timeProvider.GetLocalNow().AddHours(1))
            endDate = _timeProvider.GetLocalNow();

        var ev = new PersistentEvent
        {
            OrganizationId = organizationIds.Random(TestConstants.OrganizationId),
            ProjectId = projectIds.Random(TestConstants.ProjectId),
            ReferenceId = referenceIds.Random(),
            Date = occurrenceDate ?? RandomData.GetDateTimeOffset(startDate, endDate),
            Value = value.GetValueOrDefault() >= 0 ? value : RandomData.GetDecimal(0, Int32.MaxValue),
            StackId = stackIds.Random()!,
            Source = source
        };

        if (!String.IsNullOrEmpty(userIdentity))
            ev.SetUserIdentity(userIdentity);

        if (generateData)
        {
            ev.Data ??= new DataDictionary();
            for (int i = 0; i < RandomData.GetInt(1, 5); i++)
            {
                string key = RandomData.GetWord();
                while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                    key = RandomData.GetWord();

                ev.Data.Add(key, RandomData.GetWord());
            }
        }

        if (generateTags)
        {
            ev.Tags ??= [];
            for (int i = 0; i < RandomData.GetInt(1, 3); i++)
            {
                string? tag = TestConstants.EventTags.Random();
                if (tag is not null)
                    ev.Tags.Add(tag);
            }
        }

        if (String.IsNullOrEmpty(type) || String.Equals(type, Event.KnownTypes.Error, StringComparison.OrdinalIgnoreCase))
        {
            ev.Type = Event.KnownTypes.Error;

            // limit error variation so that stacking will occur
            if (_randomErrors is null)
                _randomErrors = [.. Enumerable.Range(1, 25).Select(i => GenerateError(maxErrorNestingLevel))];

            ev.Data ??= new DataDictionary();
            ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
        }
        else
        {
            ev.Type = type.ToLowerInvariant();
        }

        if (!String.IsNullOrEmpty(sessionId))
            ev.SetSessionId(sessionId);

        if (ev.IsSessionStart())
            ev.Value = null;

        ev.SetVersion(semver);
        return ev;
    }

    internal Error GenerateError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0)
    {
        var error = new Error
        {
            Message = "Generated exception message.",
            Type = TestConstants.ExceptionTypes.Random()
        };

        if (RandomData.GetBool())
            error.Code = RandomData.GetInt(-234523453, 98690899).ToString();

        if (generateData)
        {
            error.Data ??= new DataDictionary();
            for (int i = 0; i < RandomData.GetInt(1, 5); i++)
            {
                string key = RandomData.GetWord();
                while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                    key = RandomData.GetWord();

                error.Data.Add(key, RandomData.GetWord());
            }
        }

        var stack = new StackFrameCollection();
        for (int i = 0; i < RandomData.GetInt(1, 10); i++)
            stack.Add(GenerateStackFrame());
        error.StackTrace = stack;

        if (currentNestingLevel < maxErrorNestingLevel && RandomData.GetBool())
            error.Inner = GenerateError(maxErrorNestingLevel, generateData, currentNestingLevel + 1);

        return error;
    }

    private StackFrame GenerateStackFrame()
    {
        return new StackFrame
        {
            DeclaringNamespace = TestConstants.Namespaces.Random(),
            DeclaringType = TestConstants.TypeNames.Random(),
            Name = TestConstants.MethodNames.Random(),
            Parameters = new ParameterCollection {
                    new()
                    {
                        Type = "String",
                        Name = "path"
                    }
                }
        };
    }

    public async Task CreateSearchDataAsync(bool updateDates = false)
    {
        string path = Path.Combine("..", "..", "..", "Search", "Data");
        foreach (string file in Directory.GetFiles(path, "event*.json", SearchOption.AllDirectories))
        {
            if (file.EndsWith("summary.json"))
                continue;

            var events = _parserPluginManager.ParseEvents(await File.ReadAllTextAsync(file), 2, "exceptionless/2.0.0.0");
            Assert.NotNull(events);
            Assert.True(events.Count > 0);
            foreach (var ev in events)
            {
                if (updateDates)
                {
                    ev.Date = _timeProvider.GetLocalNow();
                    ev.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
                }

                ev.CopyDataToIndex([]);
            }

            await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());
        }

        _configuration.Events.QueryParser.Configuration.MappingResolver.RefreshMapping();
    }
}
