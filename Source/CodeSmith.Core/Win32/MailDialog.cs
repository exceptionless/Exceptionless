using System;
using System.Diagnostics;
using System.Threading;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// Represents an email message to be sent through MAPI.
    /// </summary>
    public class MailDialog
    {
        /// <summary>
        /// Specifies the valid RecipientTypes for a Recipient.
        /// </summary>
        public enum RecipientType
        {
            /// <summary>
            /// Recipient will be in the TO list.
            /// </summary>
            To = 1,

            /// <summary>
            /// Recipient will be in the CC list.
            /// </summary>
            CC = 2,

            /// <summary>
            /// Recipient will be in the BCC list.
            /// </summary>
            BCC = 3
        };

        private readonly ManualResetEvent _manualResetEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="MailDialog"/> class.
        /// </summary>
        public MailDialog()
        {
            _manualResetEvent = new ManualResetEvent(false);
            Attachments = new AttachmentCollection();
            Recipients = new RecipientCollection();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailDialog"/> class with the specified subject.
        /// </summary>
        /// <param name="subject">The subject.</param>
        public MailDialog(string subject)
            : this()
        {
            Subject = subject;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailDialog"/> class with the specified subject and body.
        /// </summary>
        /// <param name="subject">The subject.</param>
        /// <param name="body">The body.</param>
        public MailDialog(string subject, string body)
            : this()
        {
            Subject = subject;
            Body = body;
        }

        /// <summary>
        /// Occurs when the MAPI send mail call is complete.
        /// </summary>
        public event EventHandler<SendMailCompleteEventArgs> SendMailComplete;

        /// <summary>
        /// Called when send mail is complete.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        protected void OnSendMailComplete(int errorCode)
        {
            if (SendMailComplete == null)
                return;

            var eventArgs = new SendMailCompleteEventArgs(errorCode);
            SendMailComplete.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Gets or sets the subject of this mail message.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the body of this mail message.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets the recipient list for this mail message.
        /// </summary>
        public RecipientCollection Recipients { get; private set; }

        /// <summary>
        /// Gets the attachment list for this mail message.
        /// </summary>
        public AttachmentCollection Attachments { get; private set; }

        /// <summary>
        /// Displays the mail message dialog asynchronously.
        /// </summary>
        public void ShowAsync()
        {
            // Create the mail message in an STA thread
            var thread = new Thread(ShowMail);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(_manualResetEvent);

            // only return when the new thread has built it's interop representation
            _manualResetEvent.WaitOne();
            _manualResetEvent.Reset();
        }

        /// <summary>
        /// Displays the mail message dialog. The call is block until the dialog is closed.
        /// </summary>
        /// <returns>The error code from the mapi call.</returns>
        public int ShowDialog()
        {
            return ShowMail(null);
        }

        private void ShowMail(object parameter)
        {
            var manualResetEvent = parameter as ManualResetEvent;

            int error = ShowMail(manualResetEvent);

            // Check for error
            if (error > Mapi.MAPI_USER_ABORT)
            {
                string errorMessage = Mapi.GetError(error);
                Debug.WriteLine(errorMessage);
            }
        }

        private int ShowMail(EventWaitHandle waitHandle)
        {
            var message = new Mapi.MapiMessage();
            int errorCode;

            using (var recipientCollectionHandle = Recipients.GetHandle())
            using (var attachmentCollectionHandle = Attachments.GetHandle())
            {
                message.Subject = Subject;
                message.NoteText = Body;

                message.Recipients = recipientCollectionHandle;
                message.RecipientCount = Recipients.Count;

                message.Files = attachmentCollectionHandle;
                message.FileCount = Attachments.Count;

                // Signal the creating thread (make the remaining code async)
                if (waitHandle != null)
                    waitHandle.Set();

                //blocking call, waits till the mail message dialog closes
                errorCode = Mapi.MAPISendMail(IntPtr.Zero, IntPtr.Zero, message, Mapi.MAPI_DIALOG, 0);
            }

            OnSendMailComplete(errorCode);
            return errorCode;
        }
    }
}