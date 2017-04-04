using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Helpers;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Pipeline {
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
    public abstract class PipelineBase<TContext, TAction> where TAction : class, IPipelineAction<TContext> where TContext : IPipelineContext {
        protected static readonly ConcurrentDictionary<Type, IList<Type>> _actionTypeCache = new ConcurrentDictionary<Type, IList<Type>>();
        private readonly IDependencyResolver _dependencyResolver;
        private readonly IList<IPipelineAction<TContext>> _actions;
        protected readonly string _metricPrefix;
        protected readonly IMetricsClient _metricsClient;
        protected readonly ILogger _logger;

        public PipelineBase(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            _actions = GetActionTypes().Select(t => _dependencyResolver.GetService(t) as IPipelineAction<TContext>).ToList();
            _metricPrefix = String.Concat(GetType().Name.ToLower(), ".");
            _metricsClient = metricsClient ?? new InMemoryMetricsClient(loggerFactory: loggerFactory);
            _logger = loggerFactory.CreateLogger(GetType());
        }

        /// <summary>
        /// Runs all the actions of the pipeline with the specified context.
        /// </summary>
        /// <param name="context">The context to run the actions with.</param>
        public virtual async Task<TContext> RunAsync(TContext context) {
            await RunAsync(new[] { context }).AnyContext();
            return context;
        }

        /// <summary>
        /// Runs all the specified actions with the specified context.
        /// </summary>
        /// <param name="contexts">The context to run the actions with.</param>
        public virtual async Task<ICollection<TContext>> RunAsync(ICollection<TContext> contexts) {
            PipelineRunning(contexts);

            string metricPrefix = String.Concat(_metricPrefix, nameof(RunAsync).ToLower(), ".");
            foreach (var action in _actions) {
                string metricName = String.Concat(metricPrefix, action.GetType().Name.ToLower());
                var contextsToProcess = contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList();
                await _metricsClient.TimeAsync(() => action.ProcessBatchAsync(contextsToProcess), metricName).AnyContext();
                if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                    break;
            }

            contexts.ForEach(c => c.IsProcessed = c.IsCancelled == false && !c.HasError);
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
        protected virtual IList<Type> GetActionTypes() {
            return _actionTypeCache.GetOrAdd(typeof(TAction), t => TypeHelper.GetDerivedTypes<TAction>().SortByPriority());
        }
    }
}
