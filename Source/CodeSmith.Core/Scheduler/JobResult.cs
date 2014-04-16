using System;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A class representing the results from a job.
    /// </summary>
    [Serializable]
    public class JobResult : MarshalByRefObject
    {
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="JobResult"/> is cancelled.
        /// </summary>
        /// <value><c>true</c> if cancelled; otherwise, <c>false</c>.</value>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Gets or sets the error for the job.
        /// </summary>
        /// <value>The error.</value>
        public Exception Error { get; set; }

        /// <summary>
        /// Gets or sets the result of the job.
        /// </summary>
        /// <value>The result of the job.</value>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the result of the job.
        /// </summary>
        /// <value>The result of the job.</value>
        public bool IsSuccess { get; set; }

        public static readonly JobResult Cancelled = new JobResult {
            IsCancelled = true
        };

        public static readonly JobResult Success = new JobResult {
            IsSuccess = true
        };

        public static JobResult FromException(Exception exception, string message = null) {
            return new JobResult {
                Error = exception,
                IsSuccess = false,
                Message = message ?? exception.Message
            };
        }

        public static JobResult SuccessWithMessage(string message) {
            return new JobResult {
                IsSuccess = true,
                Message = message
            };
        }

        public static JobResult FailedWithMessage(string message) {
            return new JobResult {
                IsSuccess = false,
                Message = message
            };
        }
    }
}