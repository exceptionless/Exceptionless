using System;
using Exceptionless.Core.Messaging.Models;

namespace Exceptionless.Api.Hubs {
    public interface IMessageBusHubClientMethods {
        void entityChanged(EntityChanged entityChanged);
        void planChanged(PlanChanged planChanged);
        void planOverage(PlanOverage planOverage);
        void userMembershipChanged(UserMembershipChanged userMembershipChanged);
        void releaseNotification(ReleaseNotification notification);
        void systemNotification(SystemNotification notification);
    }
}