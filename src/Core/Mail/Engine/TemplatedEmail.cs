using System.Collections.Generic;
using System.Net.Mail;

namespace RazorSharpEmail {
    public class TemplatedEmail {
        public TemplatedEmail() {
            Attachments = new List<Attachment>();
        }

        public string Subject { get; set; }
        public string PlainTextBody { get; set; }
        public string HtmlBody { get; set; }
        public List<Attachment> Attachments { get; private set; }
    }
}