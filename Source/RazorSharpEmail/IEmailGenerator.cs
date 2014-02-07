using System.Net.Mail;

namespace RazorSharpEmail {
    public interface IEmailGenerator {
        MailMessage GenerateMessage<TModel>(TModel model, string templateName = null);
        TemplatedEmail Generate<TModel>(TModel model, string templateName = null);
        MailMessage GenerateMessage(TemplatedEmail templatedEmail);
    }
}