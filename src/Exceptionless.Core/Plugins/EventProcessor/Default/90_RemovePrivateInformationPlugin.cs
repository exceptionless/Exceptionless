using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(90)]
    public sealed class RemovePrivateInformationPlugin : EventProcessorPluginBase {
        public RemovePrivateInformationPlugin(ILoggerFactory loggerFactory = null) : base(loggerFactory) { }

        public override Task EventProcessingAsync(EventContext context) {
            if (context.IncludePrivateInformation)
                return Task.CompletedTask;

            context.Event.Data.Remove(Event.KnownDataKeys.UserInfo);
            var description = context.Event.GetUserDescription();

            if (description != null) {
                description.EmailAddress = null;
                if (!String.IsNullOrEmpty(description.Description))
                    context.Event.SetUserDescription(description);
                else
                    context.Event.Data.Remove(Event.KnownDataKeys.UserDescription);
            }

            return Task.CompletedTask;
        }
    }
}
