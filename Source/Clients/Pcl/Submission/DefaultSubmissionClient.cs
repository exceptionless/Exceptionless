using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Exceptionless.Models;
using Pcl;

namespace Exceptionless.Submission {
    public class DefaultSubmissionClient : ISubmissionClient {
        public Task<SubmissionResponse> SubmitAsync(IEnumerable<Error> errors, Configuration configuration) {
            throw new NotImplementedException();
        }

        public Task<SettingsResponse> GetSettingsAsync(Configuration configuration) {
            throw new NotImplementedException();
        }
    }
}
