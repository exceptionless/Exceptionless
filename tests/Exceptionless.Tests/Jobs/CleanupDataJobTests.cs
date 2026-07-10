using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Foundatio.Storage;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class CleanupDataJobTests : IntegrationTestsBase
{
    private readonly CleanupDataJob _job;
    private readonly UsageService _usageService;
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly UserData _userData;
    private readonly IUserRepository _userRepository;
    private readonly TokenData _tokenData;
    private readonly ITokenRepository _tokenRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly IFileStorage _fileStorage;

    public CleanupDataJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<CleanupDataJob>();
        _usageService = GetService<UsageService>();
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _userData = GetService<UserData>();
        _userRepository = GetService<IUserRepository>();
        _tokenData = GetService<TokenData>();
        _tokenRepository = GetService<ITokenRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
        _fileStorage = GetService<IFileStorage>();
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsSuspended = true;
        organization.SuspensionDate = DateTime.UtcNow;
        organization.SuspendedByUserId = TestConstants.UserId;
        organization.SuspensionCode = Core.Models.SuspensionCode.Billing;
        organization.SuspensionNotes = "blah";
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        await _projectRepository.AddAsync(_projectData.GenerateSampleProject());
        var token = await _tokenRepository.AddAsync(_tokenData.GenerateSampleApiKeyToken(), o => o.ImmediateConsistency());
        Assert.False(token.IsSuspended);

        await _job.RunAsync(TestCancellationToken);

        token = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.NotNull(token);
        Assert.True(token.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupExpiredDisabledOAuthTokens()
    {
        var utcNow = DateTime.UtcNow;
        var cutoff = utcNow.Subtract(TimeSpan.FromDays(1));
        var expiredSpentToken = CreateOAuthToken(cutoff.SubtractMinutes(1), isDisabled: true, refreshTokenHash: "expired-spent-refresh", refreshExpiresUtc: cutoff.SubtractMinutes(1));
        var retainedSpentToken = CreateOAuthToken(cutoff.SubtractMinutes(1), isDisabled: true, refreshTokenHash: "retained-spent-refresh", refreshExpiresUtc: cutoff.AddMinutes(1));
        var activeExpiredToken = CreateOAuthToken(cutoff.SubtractMinutes(1), isDisabled: false, refreshTokenHash: "active-expired-refresh", refreshExpiresUtc: cutoff.SubtractMinutes(1));
        var expiredClearedRefreshToken = CreateOAuthToken(cutoff.SubtractMinutes(1), isDisabled: true, refreshTokenHash: null, refreshExpiresUtc: null);
        var retainedClearedRefreshToken = CreateOAuthToken(cutoff.AddMinutes(1), isDisabled: true, refreshTokenHash: null, refreshExpiresUtc: null);

        await _oauthTokenRepository.AddAsync([
            expiredSpentToken,
            retainedSpentToken,
            activeExpiredToken,
            expiredClearedRefreshToken,
            retainedClearedRefreshToken
        ], o => o.ImmediateConsistency());

        await _oauthTokenRepository.PatchAsync(expiredClearedRefreshToken.Id, new PartialPatch(new { updated_utc = cutoff.AddMinutes(-1) }), o => o.ImmediateConsistency());
        await _oauthTokenRepository.PatchAsync(retainedClearedRefreshToken.Id, new PartialPatch(new { updated_utc = cutoff.AddMinutes(1) }), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.Null(await _oauthTokenRepository.GetByIdAsync(expiredSpentToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _oauthTokenRepository.GetByIdAsync(retainedSpentToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _oauthTokenRepository.GetByIdAsync(activeExpiredToken.Id, o => o.ImmediateConsistency()));
        Assert.Null(await _oauthTokenRepository.GetByIdAsync(expiredClearedRefreshToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _oauthTokenRepository.GetByIdAsync(retainedClearedRefreshToken.Id, o => o.ImmediateConsistency()));

        OAuthToken CreateOAuthToken(DateTime updatedUtc, bool isDisabled, string? refreshTokenHash, DateTime? refreshExpiresUtc)
        {
            return new OAuthToken
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = TestConstants.UserId,
                ClientId = "cleanup-job-oauth-client",
                GrantId = StringExtensions.GetNewToken(),
                Resource = "http://localhost:7110/mcp",
                AccessTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
                RefreshTokenHash = refreshTokenHash,
                Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
                OrganizationIds = [TestConstants.OrganizationId],
                ExpiresUtc = utcNow.AddHours(1),
                RefreshExpiresUtc = refreshExpiresUtc,
                IsDisabled = isDisabled,
                CreatedBy = TestConstants.UserId,
                CreatedUtc = updatedUtc,
                UpdatedUtc = updatedUtc
            };
        }
    }

    [Fact]
    public async Task CanCleanupSoftDeletedOrganization()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsDeleted = true;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());
        string iconPath = OrganizationStoragePaths.GetProfileImagePath(organization.Id, "icon.png");
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        await _fileStorage.SaveFileAsync(iconPath, stream, TestCancellationToken);

        await _job.RunAsync(TestCancellationToken);

        Assert.Null(await _organizationRepository.GetByIdAsync(organization.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
        Assert.False(await _fileStorage.ExistsAsync(iconPath));
    }

    [Fact]
    public async Task CleanupSyntheticOrganizationsAsync_OldSyntheticOrganization_RemovesOrganizationData()
    {
        var utcNow = TimeProvider.GetUtcNow();

        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true, name: "E2E Playwright Org cleanup-old");
        organization.CreatedUtc = utcNow.Subtract(TimeSpan.FromDays(2)).UtcDateTime;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id, name: "Playwright Project cleanup-old"), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, occurrenceDate: utcNow), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.Null(await _organizationRepository.GetByIdAsync(organization.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CleanupSyntheticOrganizationsAsync_FreshSyntheticAndSimilarOrganizations_KeepsOrganizationData()
    {
        var utcNow = TimeProvider.GetUtcNow();

        var freshSyntheticOrganization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true, name: "E2E Playwright Org cleanup-fresh");
        freshSyntheticOrganization.CreatedUtc = utcNow.Subtract(TimeSpan.FromHours(2)).UtcDateTime;
        await _organizationRepository.AddAsync(freshSyntheticOrganization, o => o.ImmediateConsistency());

        var similarOrganization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true, name: "Customer E2E Playwright Org cleanup-old");
        similarOrganization.CreatedUtc = utcNow.Subtract(TimeSpan.FromDays(2)).UtcDateTime;
        await _organizationRepository.AddAsync(similarOrganization, o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(freshSyntheticOrganization.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _organizationRepository.GetByIdAsync(similarOrganization.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CleanupSyntheticUsersAsync_OldStandaloneSyntheticUser_RemovesUserAndTokens()
    {
        var utcNow = TimeProvider.GetUtcNow();
        var user = CreateSyntheticUser("playwright-cleanup-old@exceptionless.test", "Playwright User cleanup-old", utcNow.Subtract(TimeSpan.FromDays(2)));
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        var token = await _tokenRepository.AddAsync(_tokenData.GenerateToken(
            generateId: true,
            userId: user.Id,
            organizationId: TestConstants.OrganizationId,
            type: TokenType.Authentication), o => o.ImmediateConsistency());
        var oauthToken = await _oauthTokenRepository.AddAsync(CreateUserOAuthToken(utcNow.UtcDateTime, user.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.Null(await _userRepository.GetByIdAsync(user.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _tokenRepository.GetByIdAsync(token.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _oauthTokenRepository.GetByIdAsync(oauthToken.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CleanupSyntheticUsersAsync_FreshMemberAndSimilarUsers_KeepsUsers()
    {
        var utcNow = TimeProvider.GetUtcNow();
        var freshSyntheticUser = CreateSyntheticUser("playwright-cleanup-fresh@exceptionless.test", "Playwright User cleanup-fresh", utcNow.Subtract(TimeSpan.FromHours(2)));
        var memberSyntheticUser = CreateSyntheticUser("playwright-cleanup-member@exceptionless.test", "Playwright User cleanup-member", utcNow.Subtract(TimeSpan.FromDays(2)));
        memberSyntheticUser.OrganizationIds.Add(TestConstants.OrganizationId);
        var similarUser = CreateSyntheticUser("playwright-cleanup-similar@example.com", "Playwright User cleanup-similar", utcNow.Subtract(TimeSpan.FromDays(2)));

        await _userRepository.AddAsync([freshSyntheticUser, memberSyntheticUser, similarUser], o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _userRepository.GetByIdAsync(freshSyntheticUser.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _userRepository.GetByIdAsync(memberSyntheticUser.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _userRepository.GetByIdAsync(similarUser.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupEventsOutsideOfRetentionPeriod()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var date = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, occurrenceDate: date), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanDeleteOrphanedEventsByStack()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(5000, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(10000, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());

        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(15000, eventCount);

        await GetService<CleanupOrphanedDataJob>().RunAsync(TestCancellationToken);

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(5000, eventCount);
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens_MultiTenant_OnlySuspendedOrganizationTokensAffected()
    {
        // Arrange - Organization 1 is suspended, Organization 2 is active
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        organization1.IsSuspended = true;
        organization1.SuspensionDate = DateTime.UtcNow;
        organization1.SuspendedByUserId = TestConstants.UserId;
        organization1.SuspensionCode = Core.Models.SuspensionCode.Billing;
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync(organization2, o => o.ImmediateConsistency());

        // Tokens for both organizations
        var token1 = _tokenData.GenerateToken(generateId: true, organizationId: organization1.Id, projectId: TestConstants.ProjectId);
        var token2 = _tokenData.GenerateToken(generateId: true, organizationId: organization2.Id, projectId: TestConstants.ProjectIdWithNoRoles);
        await _tokenRepository.AddAsync([token1, token2], o => o.ImmediateConsistency());

        Assert.False(token1.IsSuspended);
        Assert.False(token2.IsSuspended);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Only Organization 1's token is suspended
        var updatedToken1 = await _tokenRepository.GetByIdAsync(token1.Id);
        var updatedToken2 = await _tokenRepository.GetByIdAsync(token2.Id);
        Assert.NotNull(updatedToken1);
        Assert.NotNull(updatedToken2);
        Assert.True(updatedToken1.IsSuspended);
        Assert.False(updatedToken2.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens_AlreadySuspendedToken_RemainsUnchanged()
    {
        // Arrange - Organization suspended, token already marked as suspended
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        organization.IsSuspended = true;
        organization.SuspensionDate = DateTime.UtcNow;
        organization.SuspendedByUserId = TestConstants.UserId;
        organization.SuspensionCode = Core.Models.SuspensionCode.Abuse;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var token = _tokenData.GenerateToken(generateId: true, organizationId: organization.Id, projectId: TestConstants.ProjectId);
        token.IsSuspended = true;
        await _tokenRepository.AddAsync(token, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Token remains suspended
        var updatedToken = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens_MultipleTokensPerOrganization_AllGetSuspended()
    {
        // Arrange - Suspended organization with many tokens
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        organization.IsSuspended = true;
        organization.SuspensionDate = DateTime.UtcNow;
        organization.SuspendedByUserId = TestConstants.UserId;
        organization.SuspensionCode = Core.Models.SuspensionCode.Billing;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var tokens = new List<Token>();
        for (int i = 0; i < 10; i++)
            tokens.Add(_tokenData.GenerateToken(generateId: true, organizationId: organization.Id, projectId: TestConstants.ProjectId));
        await _tokenRepository.AddAsync(tokens, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - All 10 tokens suspended
        foreach (var t in tokens)
        {
            var updated = await _tokenRepository.GetByIdAsync(t.Id);
            Assert.NotNull(updated);
            Assert.True(updated.IsSuspended);
        }
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens_NoSuspendedOrganizations_NoTokensModified()
    {
        // Arrange - Two active organizations with tokens
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var token1 = _tokenData.GenerateToken(generateId: true, organizationId: organization1.Id, projectId: TestConstants.ProjectId);
        var token2 = _tokenData.GenerateToken(generateId: true, organizationId: organization2.Id, projectId: TestConstants.ProjectIdWithNoRoles);
        await _tokenRepository.AddAsync([token1, token2], o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - No tokens suspended
        var updated1 = await _tokenRepository.GetByIdAsync(token1.Id);
        var updated2 = await _tokenRepository.GetByIdAsync(token2.Id);
        Assert.NotNull(updated1);
        Assert.NotNull(updated2);
        Assert.False(updated1.IsSuspended);
        Assert.False(updated2.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupSoftDeletedOrganization_MultiTenant_OnlyDeletedOrganizationCleaned()
    {
        // Arrange - Organization 1 is soft-deleted, Organization 2 is active
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        organization1.IsDeleted = true;
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync(organization2, o => o.ImmediateConsistency());

        var project1 = await _projectRepository.AddAsync(_projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        var event1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id), o => o.ImmediateConsistency());
        var event2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Organization 1's entire hierarchy is hard-deleted; Organization 2's everything remains
        Assert.Null(await _organizationRepository.GetByIdAsync(organization1.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _projectRepository.GetByIdAsync(project1.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack1.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(event1.Id, o => o.IncludeSoftDeletes()));

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization2.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project2.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack2.Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event2.Id));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject_MultiTenant_OnlyDeletedProjectCleaned()
    {
        // Arrange - Two organizations, Organization 1 project is soft-deleted, Organization 2 project is active
        var organization1 = await _organizationRepository.AddAsync(_organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId), o => o.ImmediateConsistency());
        var organization2 = await _organizationRepository.AddAsync(_organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2), o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        project1.IsDeleted = true;
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        var event1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id), o => o.ImmediateConsistency());
        var event2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Project 1's stacks/events gone; Project 2 and both organizations remain
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization1.Id));
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization2.Id));
        Assert.Null(await _projectRepository.GetByIdAsync(project1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project2.Id));
        Assert.Null(await _stackRepository.GetByIdAsync(stack1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack2.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(event1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event2.Id));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack_MultiTenant_OnlyDeletedStackCleaned()
    {
        // Arrange - Same organization, two projects, one stack soft-deleted in project 1
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization.Id, projectId: project.Id);
        stack1.IsDeleted = true;
        await _stackRepository.AddAsync(stack1, o => o.ImmediateConsistency());

        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack2, o => o.ImmediateConsistency());

        var event1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack1.Id), o => o.ImmediateConsistency());
        var event2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.Null(await _stackRepository.GetByIdAsync(stack1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack2.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(event1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event2.Id));
    }

    [Fact]
    public async Task CanCleanupEventsOutsideOfRetentionPeriod_MultiTenant_OnlyExpiredEventsRemoved()
    {
        // Arrange - Two organizations on free plan, each has events inside and outside retention
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        _billingManager.ApplyBillingPlan(organization1, _plans.FreePlan);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        _billingManager.ApplyBillingPlan(organization2, _plans.FreePlan);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = await _projectRepository.AddAsync(_projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();

        // Create "will-be-expired" events at a date that's valid for index insertion
        // then advance time so they fall outside retention
        var willExpireDate = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);
        var recentDate = DateTimeOffset.UtcNow.AddDays(-1);

        // Organization 1: 1 recent (keep) + 1 at retention boundary (will be expired after time advance)
        var recentEvent1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id, occurrenceDate: recentDate), o => o.ImmediateConsistency());
        var expiredEvent1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id, occurrenceDate: willExpireDate), o => o.ImmediateConsistency());

        // Organization 2: 1 recent (keep) + 1 at retention boundary
        var recentEvent2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id, occurrenceDate: recentDate), o => o.ImmediateConsistency());
        var expiredEvent2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id, occurrenceDate: willExpireDate), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Only recent events survive (events at retention boundary are deleted)
        Assert.NotNull(await _eventRepository.GetByIdAsync(recentEvent1.Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(recentEvent2.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(expiredEvent1.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(expiredEvent2.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupEventsOutsideOfRetentionPeriod_PaidPlan_HasLongerRetention()
    {
        // Arrange - Organization 1 on free plan (short retention), Organization 2 on unlimited (long retention)
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        _billingManager.ApplyBillingPlan(organization1, _plans.FreePlan);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        _billingManager.ApplyBillingPlan(organization2, _plans.UnlimitedPlan);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = await _projectRepository.AddAsync(_projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        // Both events at the free plan retention boundary
        var options = GetService<AppOptions>();
        var dateAtFreeRetentionBoundary = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);

        var event1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id, occurrenceDate: dateAtFreeRetentionBoundary), o => o.ImmediateConsistency());
        var event2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id, occurrenceDate: dateAtFreeRetentionBoundary), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Free plan event deleted; unlimited plan event preserved
        Assert.Null(await _eventRepository.GetByIdAsync(event1.Id, o => o.IncludeSoftDeletes()));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event2.Id));
    }

    [Fact]
    public async Task FullCleanup_ComplexMultiTenantScenario_AllRulesAppliedCorrectly()
    {
        // Arrange - Complex scenario: suspended organization, soft-deleted project, soft-deleted stack, retention
        // Organization 1: suspended (tokens get suspended)
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        organization1.IsSuspended = true;
        organization1.SuspensionDate = DateTime.UtcNow;
        organization1.SuspendedByUserId = TestConstants.UserId;
        organization1.SuspensionCode = Core.Models.SuspensionCode.Billing;
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        // Organization 2: has a soft-deleted project (project and children get cleaned up)
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync(organization2, o => o.ImmediateConsistency());

        // Organization 1 token (should become suspended)
        var token1 = _tokenData.GenerateToken(generateId: true, organizationId: organization1.Id, projectId: TestConstants.ProjectId);
        await _tokenRepository.AddAsync(token1, o => o.ImmediateConsistency());

        // Organization 2 token (should remain unsuspended)
        var token2 = _tokenData.GenerateToken(generateId: true, organizationId: organization2.Id, projectId: TestConstants.ProjectIdWithNoRoles);
        await _tokenRepository.AddAsync(token2, o => o.ImmediateConsistency());

        // Project 1 for Organization 1 (active project, suspended organization)
        var project1 = await _projectRepository.AddAsync(_projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id), o => o.ImmediateConsistency());

        // Project 2 for Organization 2 (soft-deleted project)
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        project2.IsDeleted = true;
        await _projectRepository.AddAsync(project2, o => o.ImmediateConsistency());

        // Project 3 for Organization 2 (active project)
        var project3 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        // Stacks and events
        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());
        var stack3 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project3.Id), o => o.ImmediateConsistency());

        var event1 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization1.Id, project1.Id, stack1.Id), o => o.ImmediateConsistency());
        var event2 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());
        var event3 = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization2.Id, project3.Id, stack3.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        // Token 1 suspended (organization is suspended)
        var updatedToken1 = await _tokenRepository.GetByIdAsync(token1.Id);
        Assert.NotNull(updatedToken1);
        Assert.True(updatedToken1.IsSuspended);

        // Token 2 not suspended (organization is active)
        var updatedToken2 = await _tokenRepository.GetByIdAsync(token2.Id);
        Assert.NotNull(updatedToken2);
        Assert.False(updatedToken2.IsSuspended);

        // Organization 1 exists (suspended != deleted), its project/stack/event remain
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization1.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project1.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack1.Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event1.Id));

        // Organization 2 exists, soft-deleted project2 is cleaned up
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization2.Id));
        Assert.Null(await _projectRepository.GetByIdAsync(project2.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack2.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(event2.Id, o => o.IncludeSoftDeletes()));

        // Organization 2's active project3 untouched
        Assert.NotNull(await _projectRepository.GetByIdAsync(project3.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack3.Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(event3.Id));
    }

    [Fact]
    public async Task FullCleanup_NoDataToClean_CompletesSuccessfully()
    {
        // Arrange - Two healthy active organizations, no soft deletes, no suspended, no expired
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = await _projectRepository.AddAsync(_projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization2.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization1.Id, project1.Id, stack1.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization2.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        var token1 = _tokenData.GenerateToken(generateId: true, organizationId: organization1.Id, projectId: project1.Id);
        var token2 = _tokenData.GenerateToken(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _tokenRepository.AddAsync([token1, token2], o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert - Everything remains
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization1.Id));
        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization2.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project1.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project2.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack1.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack2.Id));

        var eventCount = await _eventRepository.CountAsync(o => o.ImmediateConsistency());
        Assert.Equal(20, eventCount);

        var updatedToken1 = await _tokenRepository.GetByIdAsync(token1.Id);
        var updatedToken2 = await _tokenRepository.GetByIdAsync(token2.Id);
        Assert.False(updatedToken1!.IsSuspended);
        Assert.False(updatedToken2!.IsSuspended);
    }

    [Fact]
    public async Task RemoveProjectsAsync_SoftDeletedProjectWithEvents_IncrementsDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var events = _eventData.GenerateEvents(5, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(5, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(5, orgUsage.CurrentHourUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(5, savedOrg.Usage.Sum(u => u.Deleted));

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        var allEvents = await _eventRepository.GetAllAsync();
        Assert.DoesNotContain(allEvents.Documents, e => String.Equals(e.ProjectId, project.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemoveProjectsAsync_SoftDeletedEmptyProject_DoesNotIncrementDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(0, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(0, orgUsage.CurrentHourUsage.Deleted);

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveStacksAsync_SoftDeletedStack_IncrementsDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(3, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(3, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(3, usageResponse.CurrentHourUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(3, savedOrg.Usage.Sum(u => u.Deleted));

        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveStacksAsync_MultipleProjectsSoftDeleted_TracksExactDeletedUsagePerProject()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project1 = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency());

        var stack1a = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project1.Id);
        stack1a.IsDeleted = true;
        var stack1b = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project1.Id);
        stack1b.IsDeleted = true;
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project2.Id);
        stack2.IsDeleted = true;
        await _stackRepository.AddAsync([stack1a, stack1b, stack2], o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(4, organization.Id, project1.Id, stack1a.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(2, organization.Id, project1.Id, stack1b.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(3, organization.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageProject1 = await _usageService.GetUsageAsync(organization.Id, project1.Id);
        var usageProject2 = await _usageService.GetUsageAsync(organization.Id, project2.Id);
        Assert.Equal(6, usageProject1.CurrentUsage.Deleted);
        Assert.Equal(3, usageProject2.CurrentUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(9, savedOrg.Usage.Sum(u => u.Deleted));
    }

    [Fact]
    public async Task EnforceRetentionAsync_ExpiredEvents_DoesNotIncrementDeletedUsage()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var expiredDate = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, expiredDate, expiredDate, expiredDate), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(0, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(0, usageResponse.CurrentHourUsage.Deleted);
    }

    private User CreateSyntheticUser(string emailAddress, string fullName, DateTimeOffset createdUtc)
    {
        var user = _userData.GenerateUser(generateId: true, emailAddress: emailAddress);
        user.FullName = fullName;
        user.OrganizationIds.Clear();
        user.CreatedUtc = createdUtc.UtcDateTime;
        user.UpdatedUtc = createdUtc.UtcDateTime;
        user.MarkEmailAddressVerified();
        return user;
    }

    private static OAuthToken CreateUserOAuthToken(DateTime utcNow, string userId)
    {
        return new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            ClientId = "cleanup-job-synthetic-user-client",
            GrantId = StringExtensions.GetNewToken(),
            Resource = "http://localhost:7110/mcp",
            AccessTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
            RefreshTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            OrganizationIds = [TestConstants.OrganizationId],
            ExpiresUtc = utcNow.AddHours(1),
            RefreshExpiresUtc = utcNow.AddDays(30),
            CreatedBy = userId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }
}
