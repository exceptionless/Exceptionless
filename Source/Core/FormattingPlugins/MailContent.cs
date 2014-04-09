using System;

namespace Exceptionless.Core.FormattingPlugins {
    public class MailContent {
        public string Subject { get; set; }
        public string Html { get; set; }
        public string Text { get; set; }
    }
}
