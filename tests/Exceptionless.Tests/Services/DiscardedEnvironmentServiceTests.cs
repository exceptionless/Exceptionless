using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Services;

public class DiscardedEnvironmentServiceTests : TestWithLoggingBase
{
    private const string ProjectId = "537650f3b77efe23a47914f4";
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryCacheClient _cache;
    private readonly DiscardedEnvironmentService _service;

    public DiscardedEnvironmentServiceTests(ITestOutputHelper output) : base(output)
    {
        _cache = new InMemoryCacheClient(o => o.TimeProvider(_timeProvider).LoggerFactory(Log));
        _service = new DiscardedEnvironmentService(_cache, _timeProvider);
    }

    [Fact]
    public async Task RecordAsync_WithDifferentCasing_CountsOneLowercasedKeyWithFirstSeenCasing()
    {
        // Arrange
        await _service.RecordAsync(ProjectId, ["Prodution", "prodution"]);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await _service.RecordAsync(ProjectId, ["PRODUTION"]);
        var discarded = await _service.GetAsync(ProjectId);

        // Assert
        var entry = Assert.Single(discarded);
        Assert.Equal("prodution", entry.Key);
        Assert.Equal("Prodution", entry.Name);
        Assert.Equal(3, entry.Count);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, entry.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_WithMissingEnvironments_TracksThemInTheirOwnBucket()
    {
        // Arrange
        await _service.RecordAsync(ProjectId, [null, "", "dev", null]);

        // Act
        var discarded = await _service.GetAsync(ProjectId);

        // Assert
        Assert.Equal(2, discarded.Count);
        var unspecified = Assert.Single(discarded, d => d.Key is null);
        Assert.Null(unspecified.Name);
        Assert.Equal(3, unspecified.Count);
        Assert.Equal(1, Assert.Single(discarded, d => d.Key == "dev").Count);
    }

    [Fact]
    public async Task RecordAsync_WithMoreNamesThanTheCap_TracksOnlyTheFirstNamesAndKeepsCountingKnownOnes()
    {
        // Arrange
        var names = Enumerable.Range(0, DiscardedEnvironmentService.MaximumNamesPerProject + 10).Select(i => $"env-{i}").ToList();
        foreach (string name in names)
            await _service.RecordAsync(ProjectId, [name]);

        // Act
        await _service.RecordAsync(ProjectId, ["ENV-0", "env-0"]);
        var discarded = await _service.GetAsync(ProjectId);

        // Assert
        Assert.Equal(DiscardedEnvironmentService.MaximumNamesPerProject, discarded.Count);
        Assert.DoesNotContain(discarded, d => d.Key == names[^1]);
        Assert.Equal(3, discarded.Single(d => d.Key == "env-0").Count);
    }

    [Fact]
    public async Task GetAsync_AfterTheRetentionWindow_ForgetsTheName()
    {
        // Arrange
        await _service.RecordAsync(ProjectId, ["old"]);
        _timeProvider.Advance(TimeSpan.FromDays(DiscardedEnvironmentService.RetentionDays - 1));
        await _service.RecordAsync(ProjectId, ["recent"]);

        // Act
        _timeProvider.Advance(TimeSpan.FromDays(2));
        var discarded = await _service.GetAsync(ProjectId);

        // Assert
        var entry = Assert.Single(discarded);
        Assert.Equal("recent", entry.Key);
    }

    [Fact]
    public async Task GetAsync_WithOtherProject_ReturnsOnlyThatProjectsNames()
    {
        // Arrange
        await _service.RecordAsync(ProjectId, ["dev"]);

        // Act
        var discarded = await _service.GetAsync("537650f3b77efe23a47914f5");

        // Assert
        Assert.Empty(discarded);
    }

    [Fact]
    public async Task RemoveAsync_WithDifferentCasing_ForgetsOnlyThatName()
    {
        // Arrange
        await _service.RecordAsync(ProjectId, ["Prodution", "dev", null]);

        // Act
        await _service.RemoveAsync(ProjectId, "PRODUTION");
        await _service.RemoveAsync(ProjectId, null);
        var discarded = await _service.GetAsync(ProjectId);

        // Assert
        var entry = Assert.Single(discarded);
        Assert.Equal("dev", entry.Key);
    }
}
