using System;
using Exceptionless.Core.Extensions;
using Microsoft.AspNet.SignalR;

namespace Exceptionless.Api.Hubs {
    public class PrincipalUserIdProvider : IUserIdProvider {
        public string GetUserId(IRequest request) {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.User != null && request.User.Identity != null && request.User.GetAuthType() == AuthType.User)
                return request.User.GetUserId();

            return null;
        }
    }
}
