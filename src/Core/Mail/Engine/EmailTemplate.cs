using System.Collections.Generic;
using System.Net.Mail;
using RazorEngine.Templating;

namespace RazorSharpEmail {
    public class EmailTemplate<T> : TemplateBase<T> {
        public EmailTemplate() {
            Attachments = new List<Attachment>();
        }

        public List<Attachment> Attachments { get; private set; }
    }
}