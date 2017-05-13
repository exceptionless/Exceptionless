using System;

namespace Exceptionless.Core.Pipeline
{
    /// <summary>
    /// The interface for pipeline context data
    /// </summary>
    public interface IPipelineContext {
        /// <summary>
        /// Gets or sets a value indicating whether this pipeline is cancelled.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this pipeline is cancelled; otherwise, <c>false</c>.
        /// </value>
        bool IsCancelled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this pipeline context is processed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this pipeline context is processed; otherwise, <c>false</c>.
        /// </value>
        bool IsProcessed { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not this context has gotten an error during processing.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// Used to set the context into an errored state with an error message and possibly exception with details.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="ex">The Exception that occurred.</param>
        void SetError(string message, Exception ex = null);

        /// <summary>
        /// The error message that occurred during processing.
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// Gets or sets the exception that occurred during processing of this context.
        /// </summary>
        /// <value>
        /// 	<c>Exception</c> if an error occurred during processing; otherwise, <c>null</c>.
        /// </value>
        Exception Exception { get; }
    }

    /// <summary>
    /// The base class for pipeline context data
    /// </summary>
    public abstract class PipelineContextBase : IPipelineContext {
        /// <summary>
        /// Gets or sets a value indicating whether this pipeline is cancelled.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this pipeline is cancelled; otherwise, <c>false</c>.
        /// </value>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this pipeline context is processed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this pipeline context is processed; otherwise, <c>false</c>.
        /// </value>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not this context has gotten an error during processing.
        /// </summary>
        public bool HasError => ErrorMessage != null || Exception != null;

        /// <summary>
        /// Used to set the context into an errored state with an error message and possibly exception with details.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="ex">The Exception that occurred.</param>
        public void SetError(string message, Exception ex = null) {
            ErrorMessage = message;
            Exception = ex;
        }

        /// <summary>
        /// The error message that occurred during processing.
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Gets or sets the exception that occurred during processing of this context.
        /// </summary>
        /// <value>
        /// 	<c>Exception</c> if an error occurred during processing; otherwise, <c>null</c>.
        /// </value>
        public Exception Exception { get; private set; }
    }
}