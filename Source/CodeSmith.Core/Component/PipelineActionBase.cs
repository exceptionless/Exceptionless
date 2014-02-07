using System;

namespace CodeSmith.Core.Component {
    public interface IPipelineAction<in TContext> where TContext : IPipelineContext {
        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="context">The pipeline context.</param>
        void Process(TContext context);

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
    public abstract class PipelineActionBase<TContext> : IPipelineAction<TContext> where TContext : IPipelineContext {
        protected virtual bool ContinueOnError { get { return false; } }

        /// <summary>
        /// Processes this action using the specified pipeline context.
        /// </summary>
        /// <param name="context">The pipeline context.</param>
        public abstract void Process(TContext context);

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