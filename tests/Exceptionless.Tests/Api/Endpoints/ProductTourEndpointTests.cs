using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ProductTourEndpointTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;

    public ProductTourEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_NewTour_ReturnsAndPersistsServerTimestamp()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var utcNow = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(utcNow);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(utcNow.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(utcNow.UtcDateTime, persistedUser.ProductTours["app_overview"].GetDateTime());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_RepeatedRequest_PreservesFirstTimestamp()
    {
        // Arrange
        await GetTestOrganizationUserAsync();
        var firstUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(firstUtc);
        var first = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "saved-view-create", "record")
            .StatusCodeShouldBeOk());

        // Act
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        var second = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "saved-view-create", "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.RecordedUtc, second.RecordedUtc);
        Assert.Equal(firstUtc.UtcDateTime, second.RecordedUtc);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_ClientTimestampAndPath_IgnoresClientValues()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var serverUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(serverUtc);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r
            .Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .Content(new { recorded_utc = "2000-01-01T00:00:00Z", field = "full_name" })
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(serverUtc.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(currentUser.FullName, persistedUser.FullName);
        Assert.Equal(serverUtc.UtcDateTime, persistedUser.ProductTours["app_overview"].GetDateTime());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_SequentialTours_PreservesBothDates()
    {
        // Arrange
        await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeOk());
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "exie-overview", "record")
            .StatusCodeShouldBeOk());

        // Assert
        var currentUser = await GetTestOrganizationUserAsync();
        var persistedUser = await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.True(persistedUser.ProductTours.ContainsKey("app_overview"));
        Assert.True(persistedUser.ProductTours.ContainsKey("exie_overview"));
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_NewUiDefinedTours_PreservesConcurrentUpdatesAndLegacyState()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var legacyState = JsonSerializer.SerializeToElement(new { status = "completed", updated_utc = "2024-01-15T12:00:00Z", version = 1 });
        user.ProductTours["old-tour"] = legacyState;
        await _userRepository.SaveAsync(user);
        string[] tourNames = ["future-guide-1", "future-guide-2", "future-guide-3", "future-guide-4"];

        // Act
        await Task.WhenAll(tourNames.Select(name => SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", name, "record")
            .StatusCodeShouldBeOk())));

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(5, persistedUser.ProductTours.Count);
        foreach (string name in tourNames)
        {
            Assert.True(persistedUser.ProductTours[name.Replace('-', '_')].TryGetDateTime(out _));
        }
        Assert.True(JsonElement.DeepEquals(legacyState, persistedUser.ProductTours["old-tour"]));

        var cachedUser = await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        Assert.NotNull(cachedUser);
        Assert.Equal(5, cachedUser.ProductTours.Count);
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_LegacyValueAtCurrentKey_RecordsTimestampAndPreservesOtherState()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        user.ProductTours["app_overview"] = JsonSerializer.SerializeToElement(new { status = "dismissed", updated_utc = "2024-01-15T12:00:00Z", version = 1 });
        var otherState = JsonSerializer.SerializeToElement(new { step = 3 });
        user.ProductTours["another_guide"] = otherState;
        await _userRepository.SaveAsync(user);
        var recordedUtc = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(recordedUtc);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recordedUtc.UtcDateTime, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(recordedUtc.UtcDateTime, persistedUser.ProductTours["app_overview"].GetDateTime());
        Assert.True(JsonElement.DeepEquals(otherState, persistedUser.ProductTours["another_guide"]));
    }

    [Fact]
    public async Task RecordProductTourAsync_OlderConcurrentRead_DoesNotCacheOutdatedProgress()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var repository = new PausingUserRepository(GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>());
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime;

        // Act: hold the first snapshot while a second completion is recorded and cached.
        var first = repository.RecordProductTourAsync(user, "first_guide", recordedUtc);
        try
        {
            await repository.SnapshotRead.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
            await _userRepository.RecordProductTourAsync(user, "second_guide", recordedUtc);
            await _userRepository.GetByIdAsync(user.Id, o => o.Cache());
            await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        }
        finally
        {
            repository.ResumeRead.TrySetResult();
            await first;
        }
        var byId = await _userRepository.GetByIdAsync(user.Id, o => o.Cache());
        var byEmail = await _userRepository.GetByEmailAddressAsync(user.EmailAddress);

        // Assert
        Assert.NotNull(byId);
        Assert.NotNull(byEmail);
        Assert.Equal(recordedUtc, byId.ProductTours["second_guide"].GetDateTime());
        Assert.Equal(recordedUtc, byEmail.ProductTours["second_guide"].GetDateTime());
        Assert.Equal(2, byId.ProductTours.Count);
        Assert.Equal(2, byEmail.ProductTours.Count);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(105)]
    public async Task RecordCurrentUserProductTourAsync_AtOrAboveLimit_RemovesOldestEntriesAndRecordsNewTour(int count)
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(new DateTimeOffset(recordedUtc));
        for (int i = 0; i < count; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc.AddDays(-i - 1));
        await _userRepository.SaveAsync(user);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recordedUtc, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.Equal(recordedUtc, persistedUser.ProductTours["new_guide"].GetDateTime());
        for (int i = 0; i < 99; i++)
            Assert.Equal(recordedUtc.AddDays(-i - 1), persistedUser.ProductTours[$"guide_{i}"].GetDateTime());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_ConcurrentNewKeys_RecordsEveryTourAndBoundsHistory()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime;
        for (int i = 0; i < 99; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc.AddDays(-1));
        await _userRepository.SaveAsync(user);
        string[] names = ["future-1", "future-2", "future-3", "future-4"];

        // Act
        await Task.WhenAll(names.Select(name => SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", name, "record").StatusCodeShouldBeOk())));

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        foreach (string name in names)
            Assert.True(persistedUser.ProductTours[name.Replace('-', '_')].TryGetDateTime(out _));
        Assert.Equal(96, persistedUser.ProductTours.Keys.Count(key => key.StartsWith("guide_", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    public async Task RecordCurrentUserProductTourAsync_RepeatedAtLimit_PreservesTimestampAndOtherEntries(int count)
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        for (int i = 0; i < count; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc.AddMinutes(i));
        await _userRepository.SaveAsync(user);

        // Act
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "guide-0", "record").StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recordedUtc, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(count, persistedUser.ProductTours.Count);
        foreach (var entry in user.ProductTours)
            Assert.True(JsonElement.DeepEquals(entry.Value, persistedUser.ProductTours[entry.Key]));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"not-a-timestamp\"")]
    [InlineData("{\"status\":\"completed\",\"updated_utc\":\"2024-01-15T12:00:00Z\"}")]
    public async Task RecordCurrentUserProductTourAsync_AtLimitWithLegacyValue_RemovesLegacyValueFirst(string legacyJson)
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        for (int i = 0; i < 99; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc);
        user.ProductTours["legacy"] = JsonSerializer.Deserialize<JsonElement>(legacyJson);
        await _userRepository.SaveAsync(user);

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record").StatusCodeShouldBeOk());

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.False(persistedUser.ProductTours.ContainsKey("legacy"));
        Assert.True(persistedUser.ProductTours["new_guide"].TryGetDateTime(out _));
        for (int i = 0; i < 99; i++)
            Assert.Equal(recordedUtc, persistedUser.ProductTours[$"guide_{i}"].GetDateTime());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_AtLimit_OrdersTimestampsByInstant()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        for (int i = 0; i < 98; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(TimeProvider.GetUtcNow().UtcDateTime);
        user.ProductTours["older"] = JsonSerializer.SerializeToElement("2024-01-15T08:00:00+08:00");
        user.ProductTours["newer"] = JsonSerializer.SerializeToElement("2024-01-15T01:00:00Z");
        await _userRepository.SaveAsync(user);

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record").StatusCodeShouldBeOk());

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.False(persistedUser.ProductTours.ContainsKey("older"));
        Assert.True(persistedUser.ProductTours.ContainsKey("newer"));
        Assert.True(persistedUser.ProductTours.ContainsKey("new_guide"));
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_AtLimitWithFutureDates_KeepsNewlyRecordedTour()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var futureUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(1);
        for (int i = 0; i < 100; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(futureUtc.AddMinutes(i));
        await _userRepository.SaveAsync(user);

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record").StatusCodeShouldBeOk());

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.False(persistedUser.ProductTours.ContainsKey("guide_0"));
        Assert.True(persistedUser.ProductTours.ContainsKey("new_guide"));
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_PrunedBeforeResponse_StillReturnsSuccess()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(new DateTimeOffset(recordedUtc));
        for (int i = 0; i < 100; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc.AddDays(1));
        await _userRepository.SaveAsync(user);
        var repository = Assert.IsType<UserRepository>(_userRepository);
        bool recordedLaterTour = false;
        using var handler = repository.DocumentsChanged.AddHandler(async (_, _) =>
        {
            if (recordedLaterTour)
                return;

            recordedLaterTour = true;
            await repository.RecordProductTourAsync(user, "later_guide", recordedUtc.AddDays(2));
        });

        // Act: a later completion prunes this entry before the handler reads the result.
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record").StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recordedUtc, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.False(persistedUser.ProductTours.ContainsKey("new_guide"));
        Assert.True(persistedUser.ProductTours.ContainsKey("later_guide"));
    }

    [Fact]
    public async Task GetCurrentUserAsync_TourRecordedAfterCachedRead_ReturnsRecordedProgress()
    {
        // Arrange: warm the current-user cache before recording a tour.
        var currentUser = await GetTestOrganizationUserAsync();
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime;

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeOk());
        var result = await SendRequestAsAsync<JsonElement>(r => r.AsTestOrganizationUser()
            .AppendPath("users/me").StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(recordedUtc, result.GetProperty("product_tours").GetProperty("app_overview").GetDateTime());
        var byEmail = await _userRepository.GetByEmailAddressAsync(currentUser.EmailAddress);
        Assert.NotNull(byEmail);
        Assert.Equal(recordedUtc, byEmail.ProductTours["app_overview"].GetDateTime());
    }

    [Theory]
    [InlineData("tour.name")]
    [InlineData("TourName")]
    [InlineData("tour name")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm")]
    public Task RecordCurrentUserProductTourAsync_InvalidIdentifier_ReturnsUnprocessableEntity(string name)
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", name, "record")
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_OldRoute_ReturnsNotFound()
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public Task RecordCurrentUserProductTourAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange: sample users are created by ResetDataAsync.

        // Act & Assert
        return SendRequestAsync(r => r.Put()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task RecordCurrentUserProductTourAsync_DeletedUser_ReturnsUnauthorizedWithoutRecreatingUser()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        await _userRepository.RemoveAsync(currentUser.Id, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "app-overview", "record")
            .StatusCodeShouldBeUnauthorized());

        // Assert
        Assert.Null(await _userRepository.GetByIdAsync(currentUser.Id, o => o.Cache(false)));
    }

    private async Task<ViewUser> GetTestOrganizationUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk());
        Assert.NotNull(user);
        return user;
    }

    private sealed class PausingUserRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : UserRepository(configuration, validator, options)
    {
        public TaskCompletionSource SnapshotRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<User?> GetByIdAsync(Id id, ICommandOptions? options = null)
        {
            var user = await base.GetByIdAsync(id, options);
            SnapshotRead.TrySetResult();
            await ResumeRead.Task;
            return user;
        }
    }
}
