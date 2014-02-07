using System;
using System.Collections.Generic;
using System.Linq;

#if PFX_LEGACY_3_5 || SILVERLIGHT
using CodeSmith.Core.Collections;
#else
using System.Collections.Concurrent;
using CodeSmith.Core.Dependency;

#endif

namespace CodeSmith.Core.Component
{
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
        where TContext : IPipelineContext
    {
        private static readonly ConcurrentDictionary<Type, List<Type>> _actionTypeCache;
        private readonly IDependencyResolver _dependencyResolver;

        static PipelineBase()
        {
            _actionTypeCache = new ConcurrentDictionary<Type, List<Type>>();
        }

        public PipelineBase(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
        }

        /// <summary>
        /// Runs all the modules of pipeline with the specified context list.
        /// </summary>
        /// <param name="contexts">The context list to run the modules with.</param>
        public virtual void Run(IEnumerable<TContext> contexts)
        {
            var modules = GetActionTypes();
            foreach (var context in contexts)
                Run(context, modules);
        }

        /// <summary>
        /// Runs all the modules of pipeline with the specified context.
        /// </summary>
        /// <param name="context">The context to run the modules with.</param>
        public virtual void Run(TContext context)
        {
            var modules = GetActionTypes();
            Run(context, modules);
        }

        /// <summary>
        /// Runs all the specified actions with the specified context.
        /// </summary>
        /// <param name="context">The context to run the modules with.</param>
        /// <param name="actionTypes">The ordered list of action types to run on the context.</param>
        protected virtual void Run(TContext context, IEnumerable<Type> actionTypes)
        {
            PipelineRunning(context);
            foreach (Type actionType in actionTypes) {
                var action = _dependencyResolver.GetService(actionType) as TAction;
                if (action != null) {
                    try {
                        action.Process(context);
                    } catch (Exception ex) {
                        bool cont = false;
                        try {
                            cont = action.HandleError(ex, context);
                        } catch {}

                        if (!cont)
                            throw;
                    }
                }

                if (context.IsCancelled)
                    break;
            }
            PipelineCompleted(context);
        }

        /// <summary>
        /// Called before any pipeline modules are run.
        /// </summary>
        /// <param name="context">The context the modules will run with.</param>
        protected virtual void PipelineRunning(TContext context) {}

        /// <summary>
        /// Called after all pipeline modules have run.
        /// </summary>
        /// <param name="context">The context the modules ran with.</param>
        protected virtual void PipelineCompleted(TContext context) {}

        /// <summary>
        /// Gets the types that are subclasses of <typeparamref name="TAction"/>.
        /// </summary>
        /// <returns>An enumerable list of action types in priority order to run for the pipeline.</returns>
        protected static IList<Type> GetActionTypes()
        {
            return _actionTypeCache.GetOrAdd(typeof(TAction), t =>
            {
                var modules = (from type in t.Assembly.GetTypes()
                               where (type.IsClass && !type.IsNotPublic)
                                    && !type.IsAbstract
                                    && type.IsSubclassOf(t)
                               select Tuple.Create(type, type.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute))
                               .OrderBy(i => i.Item2 != null ? i.Item2.Priority : 0)
                               .ToList();

                return modules.Select(i => i.Item1).ToList();
            });
        }
    }
}
