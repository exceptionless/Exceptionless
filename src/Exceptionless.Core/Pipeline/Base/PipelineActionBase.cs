using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Pipeline {
    public interface IPipelineAction<TContext> where TContext : IPipelineContext {
        string Name { get; }
        bool Enabled { get; }

        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="context">The pipeline context.</param>
        Task ProcessAsync(TContext context);

        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="contexts">The pipeline context.</param>
        Task ProcessBatchAsync(ICollection<TContext> contexts);

        /// <summary>
        /// Handle exceptions thrown by this action.
        /// </summary>
        /// <param name="exception">The exception that occurred while processing the action.</param>
        /// <param name="context">The pipeline context.</param>
        /// <returns>Return true if processing should continue or false if processing should halt.</returns>
        bool HandleError(Exception exception, TContext context);
    }

    /// <summary>
    /// The base class for pipeline actions
    /// </summary>
    /// <typeparam name="TContext">The type of the pipeline context.</typeparam>
    public abstract class PipelineActionBase<TContext> : IPipelineAction<TContext> where TContext : class, IPipelineContext {
        protected readonly ILogger _logger;

        public PipelineActionBase(ILoggerFactory loggerFactory = null) {
            var type = GetType();
            Name = type.Name;
            Enabled = !Settings.Current.DisabledPipelineActions.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase);
            _logger = loggerFactory?.CreateLogger(type);
        }

        public string Name { get; }

        public bool Enabled { get; }

        protected bool ContinueOnError { get; set; }

        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="context">The pipeline context.</param>
        public virtual Task ProcessAsync(TContext context) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="contexts">The pipeline context.</param>
        public virtual async Task ProcessBatchAsync(ICollection<TContext> contexts) {
            foreach (var ctx in contexts) {
                try {
                    await ProcessAsync(ctx).AnyContext();
                } catch (Exception ex) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, ctx);
                    } catch { }

                    if (!cont)
                        ctx.SetError(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Handle exceptions thrown by this action.
        /// </summary>
        /// <param name="exception">The exception that occurred while processing the action.</param>
        /// <param name="context">The pipeline context.</param>
        /// <returns>Return true if processing should continue or false if processing should halt.</returns>
        public virtual bool HandleError(Exception exception, TContext context) {
            return ContinueOnError;
        }
    }
}