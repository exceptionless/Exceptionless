using System;
using CodeSmith.Core.Scheduler;

namespace Exceptionless.Core.Extensions {
    public static class JobRunContextExtensions {
        public static JobRunContext WithWorkItemLimit(this JobRunContext context, int limit) {
            if (context == null)
                return null;

            context.Properties.Add("WorkItemLimit", limit);

            return context;
        }

        public static int GetWorkItemLimit(this JobRunContext context) {
            if (context == null)
                return -1;

            if (!context.Properties.ContainsKey("WorkItemLimit"))
                return -1;

            return Convert.ToInt32(context.Properties["WorkItemLimit"]);
        }
    }
}
