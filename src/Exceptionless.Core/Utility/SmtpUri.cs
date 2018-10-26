using System;
using System.Net;

namespace Exceptionless.Core.Utility {
    public class SmtpUri {
        public SmtpUri(string uri) : this(new Uri(uri)) { }

        public SmtpUri(Uri uri) {
            if (String.Equals(uri.Scheme, "smtps", StringComparison.OrdinalIgnoreCase)) {
                IsSecure = true;
                Port = uri.IsDefaultPort ? 465 : uri.Port;
            } else if (String.Equals(uri.Scheme, "smtp", StringComparison.OrdinalIgnoreCase)) {
                Port = uri.IsDefaultPort ? 25 : uri.Port;
            } else {
                throw new ArgumentException("Invalid SMTP scheme", nameof(uri.Scheme));
            }

            string[] parts = uri.UserInfo.Split(new [] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) {
                User = WebUtility.UrlDecode(parts[0]);
            } else if (parts.Length == 2) {
                User =  WebUtility.UrlDecode(parts[0]);
                Password = WebUtility.UrlDecode(parts[1]);
            } else if (parts.Length > 2) {
                throw new ArgumentException("Unable to parse SMTP user info", nameof(uri.UserInfo));
            }

            Host = uri.Host;
            Port = uri.IsDefaultPort ? 25 : uri.Port;
        }
        
        public string Host { get; }
        public int Port { get; }
        public bool IsSecure { get; }
        public string User { get; }
        public string Password { get; }
    }
}