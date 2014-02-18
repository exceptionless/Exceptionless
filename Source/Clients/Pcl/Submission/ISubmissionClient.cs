using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Submission {
    public interface ISubmissionClient {
        Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors);
        Task<ConfigurationResponse> GetConfigurationAsync();
    }

    public class ConfigurationResponse {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public ConfigurationDictionary Settings { get; private set; }
    }

    public class SubmissionResponse {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ConfigurationVersion { get; private set; }
    }
}
