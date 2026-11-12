using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.Jobs;

public sealed class EventNotificationsJobEnvironmentTests : IntegrationTestsBase
{
    private CountingMailer Mailer => GetService<CountingMailer>();

    public EventNotificationsJobEnvironmentTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(sp => sp.GetRequiredService<CountingMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        Mailer.Reset();
        await GetService<SampleDataService>().CreateDataAsync();

        var userRepository = GetService<IUserRepository>();
        var user = (await userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL))!;
        user.MarkEmailAddressVerified();
        await userRepository.SaveAsync(user, o => o.Cache().ImmediateConsistency());
        GetService<AppOptions>().EmailOptions.AllowedOutboundAddresses = ["exceptionless.test"];
    }

    [Fact]
    public async Task RunAsync_ForNotifyingEnvironment_SendsNewErrorNoticeOnlyAfterTheStackReachesIt()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments = [new ProjectEnvironment { Name = "dev", Notify = false }, new ProjectEnvironment { Name = "Production" }]);
        await RunPipelineAsync(project, "dev");
        await RunPipelineAsync(project, "production");

        // Act
        await GetService<EventNotificationsJob>().RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var call = Assert.Single(Mailer.EventNoticeCalls);
        Assert.True(call.IsNew);
        Assert.False(call.IsRegression);
    }

    [Fact]
    public async Task RunAsync_WhenTheEnvironmentStopsNotifyingWhileQueued_SendsNothing()
    {
        // Arrange
        var project = await ConfigureProjectAsync(p => p.Environments = [new ProjectEnvironment { Name = "Production" }]);
        await RunPipelineAsync(project, "production");
        await ConfigureProjectAsync(p => p.Environments[0].Notify = false);

        // Act
        await GetService<EventNotificationsJob>().RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        Assert.Empty(Mailer.EventNoticeCalls);
    }

    private async Task RunPipelineAsync(Project project, string environment)
    {
        var ev = GetService<EventData>().GenerateEvent(organizationId: SampleDataService.TEST_ORG_ID, projectId: SampleDataService.TEST_PROJECT_ID, type: Event.KnownTypes.Error, source: "boom", generateData: false, generateTags: false, occurrenceDate: DateTimeOffset.Now);
        ev.Tags = [];
        ev.Environment = environment;
        ev.Data = new DataDictionary { { Event.KnownDataKeys.SimpleError, new SimpleError { Message = "Boom", Type = "Test.BoomException", StackTrace = "at Boom()" } } };
        var organization = (await GetService<IOrganizationRepository>().GetByIdAsync(SampleDataService.TEST_ORG_ID))!;

        var context = await GetService<EventPipeline>().RunAsync(ev, organization, project);
        await RefreshDataAsync();

        Assert.True(context.IsProcessed, context.ErrorMessage);
    }

    private async Task<Project> ConfigureProjectAsync(Action<Project> configure)
    {
        var repository = GetService<IProjectRepository>();
        var project = (await repository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID))!;
        configure(project);
        return await repository.SaveAsync(project, o => o.ImmediateConsistency().Cache());
    }
}
