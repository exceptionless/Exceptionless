using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Submission {
    public class DefaultSubmissionClient : ISubmissionClient {
        public Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors) {
            throw new NotImplementedException();
        }

        public Task<ConfigurationResponse> GetConfigurationAsync() {
            throw new NotImplementedException();
        }
    }
}
