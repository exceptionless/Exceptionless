using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class RateNotificationRuleControllerTests : IntegrationTestsBase
{
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IUserRepository _userRepository;

    public RateNotificationRuleControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _ruleRepository = GetService<IRateNotificationRuleRepository>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    // ---- Helper: get current user via /me endpoint ----
    private async Task<ViewUser> GetTestOrganizationUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(user);
        return user;
    }

    private async Task<ViewUser> GetGlobalAdminUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(user);
        return user;
    }

    private string RuleUrl(string userId, string projectId) =>
        $"users/{userId}/projects/{projectId}/rate-notifications";

    private string RuleUrl(string userId, string projectId, string ruleId) =>
        $"users/{userId}/projects/{projectId}/rate-notifications/{ruleId}";

    // ---- CRUD tests ----

    [Fact]
    public async Task GetAsync_AsOwnUser_ReturnsList()
    {
        var user = await GetTestOrganizationUserAsync();

        var results = await SendRequestAsAsync<IReadOnlyCollection<ViewRateNotificationRule>>(r => r
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(results);
    }

    [Fact]
    public async Task GetAsync_AsDifferentUser_ReturnsNotFound()
    {
        // Using org user ID to try to access another user's rules
        var adminUser = await GetGlobalAdminUserAsync();

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()          // Org user
            .AppendPath(RuleUrl(adminUser.Id, SampleDataService.TEST_PROJECT_ID))   // accessing admin's rules
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetAsync_AsGlobalAdmin_CanAccessAnyUsersRules()
    {
        var orgUser = await GetTestOrganizationUserAsync();

        var results = await SendRequestAsAsync<IReadOnlyCollection<ViewRateNotificationRule>>(r => r
            .AsGlobalAdminUser()               // admin accessing org user's rules
            .AppendPath(RuleUrl(orgUser.Id, SampleDataService.TEST_PROJECT_ID))
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(results);
    }

    [Fact]
    public async Task PostAsync_ValidRule_CreatesRule()
    {
        var user = await GetTestOrganizationUserAsync();

        var newRule = new NewRateNotificationRule
        {
            Name = "Error spike",
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 10,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            IsEnabled = true
        };

        var created = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(newRule)
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(created);
        Assert.Equal("Error spike", created.Name);
        Assert.Equal(RateNotificationSignal.Errors, created.Signal);
        Assert.Equal(10, created.Threshold);
    }

    [Fact]
    public async Task PostAsync_InvalidWindow_Returns422()
    {
        var user = await GetTestOrganizationUserAsync();

        var newRule = new NewRateNotificationRule
        {
            Name = "Bad window",
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 10,
            Window = TimeSpan.FromMinutes(7),  // invalid — not in allowed list
            Cooldown = TimeSpan.FromMinutes(30),
            IsEnabled = true
        };

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(newRule)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostAsync_CooldownLessThanWindow_Returns422()
    {
        var user = await GetTestOrganizationUserAsync();

        var newRule = new NewRateNotificationRule
        {
            Name = "Bad cooldown",
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 10,
            Window = TimeSpan.FromMinutes(30),
            Cooldown = TimeSpan.FromMinutes(5),   // less than window
            IsEnabled = true
        };

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(newRule)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostAsync_StackSubjectWithoutStackId_Returns422()
    {
        var user = await GetTestOrganizationUserAsync();

        var newRule = new NewRateNotificationRule
        {
            Name = "Stack rule no id",
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Stack,  // stack subject
            StackId = null,                             // missing StackId
            Threshold = 10,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            IsEnabled = true
        };

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(newRule)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task GetByIdAsync_ExistingRule_ReturnsRule()
    {
        var user = await GetTestOrganizationUserAsync();

        // Create first
        var created = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(new NewRateNotificationRule
            {
                Name = "Get-by-id test",
                Signal = RateNotificationSignal.Errors,
                Subject = RateNotificationSubject.Project,
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30)
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(created);

        // Fetch by ID
        var fetched = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID, created.Id))
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Get-by-id test", fetched.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingRule_RemovesRule()
    {
        var user = await GetTestOrganizationUserAsync();

        // Create
        var created = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(new NewRateNotificationRule
            {
                Name = "Delete me",
                Signal = RateNotificationSignal.Errors,
                Subject = RateNotificationSubject.Project,
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30)
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(created);

        // Delete
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID, created.Id))
            .StatusCodeShouldBeNoContent()
        );

        // Confirm deleted
        var rule = await _ruleRepository.GetByIdAsync(created.Id);
        Assert.Null(rule);
    }

    [Fact]
    public async Task SnoozeAsync_ValidDuration_SetsSnooze()
    {
        var user = await GetTestOrganizationUserAsync();

        // Create a rule
        var created = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(new NewRateNotificationRule
            {
                Name = "Snooze test",
                Signal = RateNotificationSignal.Errors,
                Subject = RateNotificationSubject.Project,
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30)
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(created);

        // Snooze for 1 hour
        var snoozed = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath($"{RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID, created.Id)}/snooze")
            .Content(new SnoozeRateNotificationRuleRequest { DurationSeconds = 3600 })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(snoozed);
        Assert.True(snoozed.IsSnoozed, "Rule should be snoozed after snooze request.");
        Assert.NotNull(snoozed.SnoozedUntilUtc);
    }

    [Fact]
    public async Task UnsnoozeAsync_SnoozedRule_SetsSnoozedUntilToNow()
    {
        var user = await GetTestOrganizationUserAsync();

        // Create + snooze
        var created = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(new NewRateNotificationRule
            {
                Name = "Unsnooze test",
                Signal = RateNotificationSignal.Errors,
                Subject = RateNotificationSubject.Project,
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30)
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(created);

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath($"{RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID, created.Id)}/snooze")
            .Content(new SnoozeRateNotificationRuleRequest { DurationSeconds = 3600 })
            .StatusCodeShouldBeOk()
        );

        // Unsnooze — sets SnoozedUntilUtc = now, so IsSnoozed = false
        var unsnoozed = await SendRequestAsAsync<ViewRateNotificationRule>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath($"{RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID, created.Id)}/unsnooze")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(unsnoozed);
        Assert.False(unsnoozed.IsSnoozed, "Rule should NOT be actively snoozed after unsnooze (SnoozedUntilUtc = now).");
        // SnoozedUntilUtc is still set (to now) — not null. This is the fresh baseline mechanism.
        Assert.NotNull(unsnoozed.SnoozedUntilUtc);
    }

    [Fact]
    public async Task PostAsync_ExceedsMaxRulesPerUser_Returns422()
    {
        var user = await GetTestOrganizationUserAsync();

        // Create 20 rules (the maximum)
        for (int i = 0; i < 20; i++)
        {
            await SendRequestAsync(r => r
                .Post()
                .AsTestOrganizationUser()
                .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
                .Content(new NewRateNotificationRule
                {
                    Name = $"Rule {i + 1}",
                    Signal = RateNotificationSignal.Errors,
                    Subject = RateNotificationSubject.Project,
                    Threshold = 5,
                    Window = TimeSpan.FromMinutes(5),
                    Cooldown = TimeSpan.FromMinutes(30)
                })
                .StatusCodeShouldBeCreated()
            );
        }

        // 21st rule should fail
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath(RuleUrl(user.Id, SampleDataService.TEST_PROJECT_ID))
            .Content(new NewRateNotificationRule
            {
                Name = "Rule 21",
                Signal = RateNotificationSignal.Errors,
                Subject = RateNotificationSubject.Project,
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30)
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }
}
