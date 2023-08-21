﻿using System.Collections.Concurrent;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

/// <summary>
/// The base class for a pipeline service.
/// </summary>
/// <typeparam name="TContext">The type used as the context for the pipeline.</typeparam>
/// <typeparam name="TAction">The base type of the pipeline action to run in this pipeline.</typeparam>
/// <remarks>
/// The pipeline works by executing actions (classes) that have a common base class in a series.
/// To setup a pipeline, you have to have a context class that will hold all the common data for the pipeline.
/// You also have to have a common base class that inherits <see cref="IPipelineContext"/> for all your actions.
/// The pipeline looks for all types that inherit that action base class to run.
/// </remarks>
public abstract class PipelineBase<TContext, TAction> where TAction : class, IPipelineAction<TContext> where TContext : IPipelineContext
{
    protected static readonly ConcurrentDictionary<Type, IList<Type>> _actionTypeCache = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly AppOptions _options;
    private readonly IList<IPipelineAction<TContext>> _actions;
    protected readonly string _metricPrefix;
    protected readonly ILogger _logger;

    public PipelineBase(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _options = options;

        var type = GetType();
        _metricPrefix = String.Concat(type.Name.ToLower(), ".");
        _logger = loggerFactory.CreateLogger(type);

        _actions = LoadDefaultActions();
    }

    /// <summary>
    /// Runs all the actions of the pipeline with the specified context.
    /// </summary>
    /// <param name="context">The context to run the actions with.</param>
    public virtual async Task<TContext> RunAsync(TContext context)
    {
        await RunAsync(new[] { context }).AnyContext();
        return context;
    }

    /// <summary>
    /// Runs all the specified actions with the specified context.
    /// </summary>
    /// <param name="contexts">The context to run the actions with.</param>
    public virtual async Task<ICollection<TContext>> RunAsync(ICollection<TContext> contexts)
    {
        PipelineRunning(contexts);

        foreach (var action in _actions)
        {
            string metricName = String.Concat(_metricPrefix, action.Name.ToLower());
            var contextsToProcess = contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList();
            await AppDiagnostics.TimeAsync(() => action.ProcessBatchAsync(contextsToProcess), metricName).AnyContext();
            if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                break;
        }

        contexts.ForEach(c => c.IsProcessed = c.IsCancelled == false && c.HasError == false);
        PipelineCompleted(contexts);

        return contexts;
    }

    /// <summary>
    /// Called before any pipeline modules are run.
    /// </summary>
    /// <param name="contexts">The context the modules will run with.</param>
    protected virtual void PipelineRunning(ICollection<TContext> contexts) { }

    /// <summary>
    /// Called after all pipeline modules have run.
    /// </summary>
    /// <param name="contexts">The context the modules ran with.</param>
    protected virtual void PipelineCompleted(ICollection<TContext> contexts) { }

    /// <summary>
    /// Gets the types that are subclasses of <typeparamref name="TAction"/>.
    /// </summary>
    /// <returns>An enumerable list of action types in priority order to run for the pipeline.</returns>
    protected virtual IList<Type> GetActionTypes()
    {
        return _actionTypeCache.GetOrAdd(typeof(TAction), t => TypeHelper.GetDerivedTypes<TAction>().SortByPriority());
    }

    private List<IPipelineAction<TContext>> LoadDefaultActions()
    {
        var actions = new List<IPipelineAction<TContext>>();
        foreach (var type in GetActionTypes())
        {
            if (_options.DisabledPipelineActions.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning("Pipeline Action {Name} is currently disabled and won't be executed.", type.Name);
                continue;
            }

            try
            {
                actions.Add((IPipelineAction<TContext>)_serviceProvider.GetRequiredService(type));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to instantiate Pipeline Action of type {TypeFullName}: {Message}", type.FullName, ex.Message);
                throw;
            }
        }

        return actions;
    }
}
