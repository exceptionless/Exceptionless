using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Submission {
    public interface ISubmissionClient {
        Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors, Configuration configuration);
        Task<SettingsResponse> GetSettingsAsync(Configuration configuration);
    }

    public class SettingsResponse {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public SettingsDictionary Settings { get; private set; }
        public int SettingsVersion { get; private set; }
    }

    public class SubmissionResponse {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public int SettingsVersion { get; private set; }
    }
}
