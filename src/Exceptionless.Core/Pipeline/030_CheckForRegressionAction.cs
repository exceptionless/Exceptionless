using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using McSherry.SemanticVersioning;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(30)]
public class CheckForRegressionAction : EventPipelineActionBase
{
    private readonly IStackRepository _stackRepository;
    private readonly SemanticVersionParser _semanticVersionParser;

    public CheckForRegressionAction(IStackRepository stackRepository, SemanticVersionParser semanticVersionParser, AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory)
    {
        _stackRepository = stackRepository;
        _semanticVersionParser = semanticVersionParser;
        ContinueOnError = true;
    }

    public override async Task ProcessBatchAsync(ICollection<EventContext> contexts)
    {
        var stacks = contexts.Where(c => c.Stack.Status != StackStatus.Regressed && c.Stack.DateFixed.HasValue).OrderBy(c => c.Event.Date).GroupBy(c => c.Event.StackId);

        // more than one event in this batch, likely to be the same version
        Dictionary<string, SemanticVersion> versionCache = null;
        if (contexts.Count > 1)
            versionCache = new Dictionary<string, SemanticVersion>();

        foreach (var stackGroup in stacks)
        {
            try
            {
                var stack = stackGroup.First().Stack;

                EventContext regressedContext = null;
                SemanticVersion regressedVersion = null;
                if (String.IsNullOrEmpty(stack.FixedInVersion))
                {
                    regressedContext = stackGroup.FirstOrDefault(c => stack.DateFixed < c.Event.Date.UtcDateTime);
                }
                else
                {
                    var fixedInVersion = _semanticVersionParser.Parse(stack.FixedInVersion, versionCache);
                    var versions = stackGroup.GroupBy(c => c.Event.GetVersion());
                    foreach (var versionGroup in versions)
                    {
                        var version = _semanticVersionParser.Parse(versionGroup.Key, versionCache) ?? _semanticVersionParser.Default;
                        if (version < fixedInVersion)
                            continue;

                        regressedVersion = version;
                        regressedContext = versionGroup.First();
                        break;
                    }
                }

                if (regressedContext == null)
                    return;

                _logger.LogTrace("Marking stack and events as regressed in version: {Version}", regressedVersion);
                stack.Status = StackStatus.Regressed;
                await _stackRepository.MarkAsRegressedAsync(stack.Id).AnyContext();

                foreach (var ctx in stackGroup)
                    ctx.IsRegression = ctx == regressedContext;
            }
            catch (Exception ex)
            {
                foreach (var context in stackGroup)
                {
                    bool cont = false;
                    try
                    {
                        cont = HandleError(ex, context);
                    }
                    catch { }

                    if (!cont)
                        context.SetError(ex.Message, ex);
                }
            }
        }
    }

    public override Task ProcessAsync(EventContext ctx)
    {
        return Task.CompletedTask;
    }
}
