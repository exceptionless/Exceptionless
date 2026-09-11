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
    public async Task RecordCurrentUserProductTourAsync_AtOrAboveLimit_PreservesExistingEntries(int count)
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc);
        await _userRepository.SaveAsync(user);

        // Act
        await SendRequestAsync(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "new-guide", "record")
            .StatusCodeShouldBeUnprocessableEntity());
        var result = await SendRequestAsAsync<RecordProductTourResult>(r => r.Put().AsTestOrganizationUser()
            .AppendPaths("users", "me", "product-tours", "guide-0", "record")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recordedUtc, result.RecordedUtc);
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(count, persistedUser.ProductTours.Count);
        Assert.False(persistedUser.ProductTours.ContainsKey("new_guide"));
    }

    [Fact]
    public async Task RecordProductTourAsync_ConcurrentNewKeys_EnforcesLimitAtomically()
    {
        // Arrange
        var currentUser = await GetTestOrganizationUserAsync();
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime;
        for (int i = 0; i < 99; i++)
            user.ProductTours[$"guide_{i}"] = JsonSerializer.SerializeToElement(recordedUtc);
        await _userRepository.SaveAsync(user);
        string[] names = ["future_1", "future_2", "future_3", "future_4"];

        // Act
        await Task.WhenAll(names.Select(name => _userRepository.RecordProductTourAsync(user, name, recordedUtc)));

        // Assert
        var persistedUser = await _userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        Assert.NotNull(persistedUser);
        Assert.Equal(100, persistedUser.ProductTours.Count);
        Assert.Single(names, persistedUser.ProductTours.ContainsKey);
        Assert.Equal(recordedUtc, persistedUser.ProductTours["guide_0"].GetDateTime());
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
