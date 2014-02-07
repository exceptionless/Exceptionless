using System;
using System.Runtime.InteropServices;
using System.Security;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// Internal class for calling MAPI APIs
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static class Mapi
    {
        public const int MAPI_LOGON_UI = 0x1;
        public const int MAPI_DIALOG = 0x8;
        
        // errors
        public const int SUCCESS_SUCCESS = 0;
        public const int MAPI_USER_ABORT = 1;
        public const int MAPI_E_FAILURE = 2;
        public const int MAPI_E_LOGIN_FAILURE = 3;
        public const int MAPI_E_DISK_FULL = 4;
        public const int MAPI_E_INSUFFICIENT_MEMORY = 5;
        public const int MAPI_E_BLK_TOO_SMALL = 6;
        public const int MAPI_E_TOO_MANY_SESSIONS = 8;
        public const int MAPI_E_TOO_MANY_FILES = 9;
        public const int MAPI_E_TOO_MANY_RECIPIENTS = 10;
        public const int MAPI_E_ATTACHMENT_NOT_FOUND = 11;
        public const int MAPI_E_ATTACHMENT_OPEN_FAILURE = 12;
        public const int MAPI_E_ATTACHMENT_WRITE_FAILURE = 13;
        public const int MAPI_E_UNKNOWN_RECIPIENT = 14;
        public const int MAPI_E_BAD_RECIPTYPE = 15;
        public const int MAPI_E_NO_MESSAGES = 16;
        public const int MAPI_E_INVALID_MESSAGE = 17;
        public const int MAPI_E_TEXT_TOO_LARGE = 18;
        public const int MAPI_E_INVALID_SESSION = 19;
        public const int MAPI_E_TYPE_NOT_SUPPORTED = 20;
        public const int MAPI_E_AMBIGUOUS_RECIPIENT = 21;
        public const int MAPI_E_MESSAGE_IN_USE = 22;
        public const int MAPI_E_NETWORK_FAILURE = 23;
        public const int MAPI_E_INVALID_EDITFIELDS = 24;
        public const int MAPI_E_INVALID_RECIPS = 25;
        public const int MAPI_E_NOT_SUPPORTED = 26;
        public const int MAPI_E_NO_LIBRARY = 999;
        public const int MAPI_E_INVALID_PARAMETER = 998;

        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        public static extern int MAPILogon(IntPtr hwnd, string prf, string pw, int flg, int rsv, ref IntPtr sess);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiFileDescriptor
        {
            public int Reserved;        /* Reserved for future use (must be 0)              */
            public int Flags;           /* Flags                                            */
            public int Position;        /* character in text to be replaced by attachment   */
            public string PathName;     /* Full path name of attachment file                */
            public string FileName;     /* Original file name (optional)                    */
            public int FileType;        /* Attachment file type (can be lpMapiFileTagExt)   */
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiMessage
        {
            public int Reserved;            /* Reserved for future use (M.B. 0)       */
            public string Subject;          /* Message Subject                        */
            public string NoteText;         /* Message Text                           */
            public string MessageType;      /* Message Class                          */
            public string DateReceived;     /* in YYYY/MM/DD HH:MM format             */
            public string ConversationID;   /* conversation thread ID                 */
            public int Flags;               /* unread,return receipt                  */
            public IntPtr Originator;       /* Originator descriptor                  */
            public int RecipientCount;      /* Number of recipients                   */
            public IntPtr Recipients;       /* Recipient descriptors                  */
            public int FileCount;           /* # of file attachments                  */
            public IntPtr Files;            /* Attachment descriptors                 */
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiRecipDesc
        {
            public int Reserved;             /* Reserved for future use                                  */
            public int RecipientClass;       /* Recipient class MAPI_TO, MAPI_CC, MAPI_BCC, MAPI_ORIG    */
            public string Name;              /* Recipient name                                           */
            public string Address;           /* Recipient address (optional)                             */
            public int EIDSize;              /* Count in bytes of size of pEntryID                       */
            public IntPtr EntryID;           /* System-specific recipient reference                      */
        }

        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        public static extern int MAPISendMail(IntPtr session, IntPtr hwnd, MapiMessage message, int flg, int rsv);


        /// <summary>
        /// Gets the error message from the specified error code.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The error message from the error code.</returns>
        public static string GetError(int errorCode)
        {            
            switch (errorCode)
            {
                case MAPI_USER_ABORT:
                    return "User Aborted.";
                case MAPI_E_FAILURE:
                    return "MAPI Failure.";
                case MAPI_E_LOGIN_FAILURE:
                    return "Login Failure.";
                case MAPI_E_DISK_FULL:
                    return "MAPI Disk full.";
                case MAPI_E_INSUFFICIENT_MEMORY:
                    return "MAPI Insufficient memory.";
                case MAPI_E_BLK_TOO_SMALL:
                    return "MAPI Block too small.";
                case MAPI_E_TOO_MANY_SESSIONS:
                    return "MAPI Too many sessions.";
                case MAPI_E_TOO_MANY_FILES:
                    return "MAPI too many files.";
                case MAPI_E_TOO_MANY_RECIPIENTS:
                    return "MAPI too many recipients.";
                case MAPI_E_ATTACHMENT_NOT_FOUND:
                    return "MAPI Attachment not found.";
                case MAPI_E_ATTACHMENT_OPEN_FAILURE:
                    return "MAPI Attachment open failure.";
                case MAPI_E_ATTACHMENT_WRITE_FAILURE:
                    return "MAPI Attachment Write Failure.";
                case MAPI_E_UNKNOWN_RECIPIENT:
                    return "MAPI Unknown recipient.";
                case MAPI_E_BAD_RECIPTYPE:
                    return "MAPI Bad recipient type.";
                case MAPI_E_NO_MESSAGES:
                    return "MAPI No messages.";
                case MAPI_E_INVALID_MESSAGE:
                    return "MAPI Invalid message.";
                case MAPI_E_TEXT_TOO_LARGE:
                    return "MAPI Text too large.";
                case MAPI_E_INVALID_SESSION:
                    return "MAPI Invalid session.";
                case MAPI_E_TYPE_NOT_SUPPORTED:
                    return "MAPI Type not supported.";
                case MAPI_E_AMBIGUOUS_RECIPIENT:
                    return "MAPI Ambiguous recipient.";
                case MAPI_E_MESSAGE_IN_USE:
                    return "MAPI Message in use.";
                case MAPI_E_NETWORK_FAILURE:
                    return "MAPI Network failure.";
                case MAPI_E_INVALID_EDITFIELDS:
                    return "MAPI Invalid edit fields.";
                case MAPI_E_INVALID_RECIPS:
                    return "MAPI Invalid Recipients.";
                case MAPI_E_NOT_SUPPORTED:
                    return "MAPI Not supported.";
                case MAPI_E_NO_LIBRARY:
                    return "MAPI No Library.";
                case MAPI_E_INVALID_PARAMETER:
                    return "MAPI Invalid parameter.";
            }
            return String.Empty;
        }

    }
}