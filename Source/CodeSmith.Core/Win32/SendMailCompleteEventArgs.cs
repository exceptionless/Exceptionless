using System;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// A class representing the SendMailComplate event.
    /// </summary>
    public class SendMailCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the error code.
        /// </summary>
        /// <value>The error code.</value>
        public int ErrorCode
        {
            get; private set;
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string ErrorMessage
        {
            get { return Mapi.GetError(ErrorCode); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SendMailCompleteEventArgs"/> class.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        public SendMailCompleteEventArgs(int errorCode)
        {
            ErrorCode = errorCode;
        }
    }
}