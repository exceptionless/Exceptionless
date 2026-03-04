using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Mapping;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;

namespace Exceptionless.Web;

public class Bootstrapper
{
    public static void RegisterServices(IServiceCollection services, AppOptions appOptions, ILoggerFactory loggerFactory)
    {
        services.AddSingleton<WebSocketConnectionManager>();
        services.AddSingleton<MessageBusBroker>();

        services.AddSingleton<ApiMapper>();

        Core.Bootstrapper.RegisterServices(services, appOptions);
        Insulation.Bootstrapper.RegisterServices(services, appOptions, appOptions.RunJobsInProcess);

        if (appOptions.RunJobsInProcess)
            Core.Bootstrapper.AddHostedJobs(services, loggerFactory);

        var logger = loggerFactory.CreateLogger<Startup>();
        services.AddStartupAction<MessageBusBroker>();
        services.AddStartupAction("Subscribe to Log Work Item Progress", (sp, ct) =>
        {
            var subscriber = sp.GetRequiredService<IMessageSubscriber>();
            return subscriber.SubscribeAsync<WorkItemStatus>(workItemStatus =>
            {
                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace("WorkItem id:{WorkItemId} message:{Message} progress:{Progress}", workItemStatus.WorkItemId ?? "<NULL>", workItemStatus.Message ?? "<NULL>", workItemStatus.Progress);

                return Task.CompletedTask;
            }, ct);
        });

        services.AddSingleton<EnqueueOrganizationNotificationOnPlanOverage>();
        services.AddStartupAction<EnqueueOrganizationNotificationOnPlanOverage>();
    }
}
