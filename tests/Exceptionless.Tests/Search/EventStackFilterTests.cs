using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories;

public sealed class EventStackFilterTests : IntegrationTestsBase
{
    private readonly StackData _stackData;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;

    public EventStackFilterTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        TimeProvider.SetUtcNow(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
        _stackData = GetService<StackData>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();

        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();

        var oldLoggingLevel = Log.DefaultLogLevel;
        Log.DefaultLogLevel = LogLevel.Warning;

        await _stackData.CreateSearchDataAsync();
        await _eventData.CreateSearchDataAsync();

        Log.DefaultLogLevel = oldLoggingLevel;
    }

    [Theory]
    [InlineData("status:fixed", 2)]
    [InlineData("status:regressed", 3)]
    [InlineData("status:open", 1)]
    public async Task GetByStatusAsync(string filter, int count)
    {
        var result = await GetAsync(filter);
        Assert.NotNull(result);
        Assert.Equal(count, result.Total);
    }

    [Theory]
    [InlineData("status:open", 1)]
    [InlineData("status:regressed", 3)]
    [InlineData("status:ignored", 1)]
    [InlineData("(status:open OR status:regressed)", 4)]
    [InlineData("is_fixed:true", 2)]
    [InlineData("status:fixed", 2)]
    [InlineData("status:discarded", 0)]
    [InlineData("tags:old_tag", 0)] // Stack only tags won't be resolved
    [InlineData("type:log status:fixed", 2)]
    [InlineData("type:log version_fixed:1.2.3", 1)]
    [InlineData("type:error is_hidden:false is_fixed:false is_regressed:true", 2)]
    [InlineData("type:log status:fixed version_fixed:1.2.3", 1)]
    [InlineData("54dbc16ca0f5c61398427b00", 1)] // Event Id
    [InlineData("1ecd0826e447a44e78877ab1", 0)] // Stack Id
    [InlineData("type:error", 2)]
    public async Task GetByFilterAsync(string filter, int count)
    {
        var result = await GetAsync(filter);
        Assert.NotNull(result);
        Assert.Equal(count, result.Total);
    }

    private Task<FindResults<PersistentEvent>> GetAsync(string filter)
    {
        return _eventRepository.FindAsync(q => q.FilterExpression(filter).EnforceEventStackFilter(), o => o.QueryLogLevel(LogLevel.Information));
    }
}
