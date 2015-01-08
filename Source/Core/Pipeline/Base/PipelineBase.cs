using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Helpers;

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
    public abstract class PipelineBase<TContext, TAction>
        where TAction : class, IPipelineAction<TContext>
        where TContext : IPipelineContext {
        protected static readonly ConcurrentDictionary<Type, IList<Type>> _actionTypeCache;
        protected static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, IPipelineAction<TContext>>> _actionCache;
        private readonly IDependencyResolver _dependencyResolver;

        static PipelineBase() {
            _actionTypeCache = new ConcurrentDictionary<Type, IList<Type>>();
            _actionCache = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, IPipelineAction<TContext>>>();
        }

        public PipelineBase(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
        }

        /// <summary>
        /// Runs all the actions of the pipeline with the specified context.
        /// </summary>
        /// <param name="context">The context to run the actions with.</param>
        public virtual TContext Run(TContext context) {
            var actionTypes = GetActionTypes();
            return Run(new[] { context }, actionTypes).First();
        }

        /// <summary>
        /// Runs all the actions of the pipeline with the specified context list.
        /// </summary>
        /// <param name="contexts">The context list to run the actions with.</param>
        public virtual ICollection<TContext> Run(ICollection<TContext> contexts) {
            var actionTypes = GetActionTypes();
            return Run(contexts, actionTypes);
        }

        /// <summary>
        /// Runs all the specified actions with the specified context.
        /// </summary>
        /// <param name="contexts">The context to run the actions with.</param>
        /// <param name="actionTypes">The ordered list of action types to run on the context.</param>
        protected virtual ICollection<TContext> Run(ICollection<TContext> contexts, IEnumerable<Type> actionTypes) {
            PipelineRunning(contexts);

            var cache = _actionCache.GetOrAdd(typeof(TAction), new ConcurrentDictionary<Type, IPipelineAction<TContext>>());
            foreach (Type actionType in actionTypes) {
                var action = cache.GetOrAdd(actionType, _dependencyResolver.GetService(actionType) as TAction);
                if (action != null)
                    action.ProcessBatch(contexts.Where(c => c.IsCancelled == false && c.Exception == null).ToList());

                if (contexts.All(c => c.IsCancelled))
                    break;
            }

            contexts.ForEach(c => c.IsProcessed = c.IsCancelled == false && c.Exception == null);

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
