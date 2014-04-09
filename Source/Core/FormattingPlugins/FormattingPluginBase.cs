using System;
using Exceptionless.Core.EventPlugins;

namespace Exceptionless.Core.FormattingPlugins {
    public abstract class FormattingPluginBase : IFormattingPlugin {
        public MailContent GetEventMailNotificationContent(EventContext context) {
            throw new NotImplementedException();
        }
    }
}
