using System;
using System.Diagnostics;

namespace Exceptionless.Core.Models.Data {
    [DebuggerDisplay("Ip Address: {IpAddress}, User Agent: {UserAgent}, Version: {Version}")]
    public class SubmissionClient {
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Version { get; set; }
    }

    public static class SubmissionClientExtensions {
        public static bool IsDotNetClient(this SubmissionClient submissionClient) {
            return submissionClient?.UserAgent?.StartsWith("exceptionless/") ?? false;
        }

        public static bool IsJavaScriptClient(this SubmissionClient submissionClient) {
            return submissionClient?.UserAgent?.StartsWith("exceptionless-js/") ?? false;
        }

        public static bool IsJavaScriptUniversalClient(this SubmissionClient submissionClient) {
            return submissionClient?.UserAgent?.StartsWith("exceptionless-universal-js/") ?? false;
        }

        public static bool IsJavaScriptNodeClient(this SubmissionClient submissionClient) {
            return submissionClient?.UserAgent?.StartsWith("exceptionless-node/") ?? false;
        }
    }
}