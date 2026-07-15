using Exceptionless.Core.Extensions;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Services.SourceMaps;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default;

[Priority(15)]
public sealed class SourceMapPlugin : EventProcessorPluginBase
{
    private readonly SourceMapService _sourceMapService;
    private readonly ITextSerializer _serializer;
    private readonly BillingPlans _billingPlans;

    public SourceMapPlugin(SourceMapService sourceMapService, ITextSerializer serializer, BillingPlans billingPlans, AppOptions options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
        _sourceMapService = sourceMapService;
        _serializer = serializer;
        _billingPlans = billingPlans;
        ContinueOnError = true;
    }

    public override async Task EventProcessingAsync(EventContext context)
    {
        if (!context.Event.IsError())
            return;

        var error = context.Event.GetError(_serializer, _logger);
        if (error is null)
            return;

        var request = new SourceMapRequest(
            context.Organization.Id,
            context.Project.Id,
            context.EventPostInfo?.ClientKeyHash,
            String.Equals(context.Organization.PlanId, _billingPlans.FreePlan.Id, StringComparison.OrdinalIgnoreCase));
        if (await _sourceMapService.SymbolicateAsync(request, error))
            context.Event.SetError(error);
    }
}
