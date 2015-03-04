using System;
using System.Net.Mail;
using System.Reflection;
using Exceptionless.Core.Extensions;
using RazorEngine.Configuration;
using RazorEngine.Templating;

#pragma warning disable 618

namespace RazorSharpEmail {
    public class RazorEmailGenerator : IEmailGenerator, IDisposable {
        private readonly ITemplateService _templateService;

        /// <summary>
        /// Use embedded resource templates based on the specified type.
        /// </summary>
        /// <param name="templateModelType">A type that will be used to find the embedded template resources. If the type's namespace ends in ".Models", then ".Models" will be replaced with ".Templates".</param>
        public RazorEmailGenerator(Type templateModelType) {
            string templateNamespace = templateModelType.Namespace ?? String.Empty;
            if (templateNamespace.EndsWith(".Models"))
                templateNamespace = templateNamespace.Replace(".Models", ".Templates");

            _templateService = new TemplateService(new TemplateServiceConfiguration {
                Resolver = new EmbeddedTemplateResolver(templateModelType.Assembly, templateNamespace)
            });
        }

        /// <summary>
        /// Use embedded resource templates located in the specified assembly and namespace.
        /// </summary>
        /// <param name="templateAssembly">The assembly that the embedded templates are located in.</param>
        /// <param name="templateNamespace">The namespace that the embedded templates are located in.</param>
        public RazorEmailGenerator(Assembly templateAssembly, string templateNamespace) {
            _templateService = new TemplateService(new TemplateServiceConfiguration {
                Resolver = new EmbeddedTemplateResolver(templateAssembly, templateNamespace)
            });
        }

        /// <summary>
        /// Use directory based templates.
        /// </summary>
        /// <param name="directory">The directory that the templates are located in.</param>
        public RazorEmailGenerator(string directory = null) {
            _templateService = new TemplateService(new TemplateServiceConfiguration {
                Resolver = new DirectoryTemplateResolver(directory)
            });
        }

        /// <summary>
        /// Use a custom template service for rendering templates.
        /// </summary>
        /// <param name="templateService">The template service to use for resolving and rendering templates.</param>
        public RazorEmailGenerator(ITemplateService templateService) {
            _templateService = templateService;
        }

        public MailMessage GenerateMessage<TModel>(TModel model, string templateName = null) {
            return GenerateMessage(Generate(model, templateName));
        }

        public TemplatedEmail Generate<TModel>(TModel model, string templateName = null) {
            var language = GetCurrentLanguage();

            var templatedEmail = new TemplatedEmail();
            if (String.IsNullOrEmpty(templateName))
                templateName = typeof(TModel).Name;

            if (language.Length > 0)
                templateName = "{0}.{1}".FormatWith(language, templateName);

            var subjectTemplate = _templateService.Resolve("{0}\\Subject.cshtml".FormatWith(templateName), model);
            var plainTextBodyTemplate = _templateService.Resolve("{0}\\PlainText.cshtml".FormatWith(templateName), model);
            var htmlBodyTemplate = _templateService.Resolve("{0}\\Html.cshtml".FormatWith(templateName), model);

            templatedEmail.Subject = _templateService.Run(subjectTemplate, null).HtmlEntityDecode().RemoveLineFeeds();
            dynamic viewBag = new DynamicViewBag();
            viewBag.Subject = templatedEmail.Subject;
            templatedEmail.PlainTextBody = ((string)_templateService.Run(plainTextBodyTemplate, viewBag)).HtmlEntityDecode();
            templatedEmail.HtmlBody = _templateService.Run(htmlBodyTemplate, viewBag);

            return templatedEmail;
        }

        public MailMessage GenerateMessage(TemplatedEmail templatedEmail) {
            // Create the mail message and set the subject
            var mailMessage = new MailMessage { Subject = templatedEmail.Subject };

            var plainTextView = AlternateView.CreateAlternateViewFromString(templatedEmail.PlainTextBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(templatedEmail.HtmlBody, null, "text/html");

            // Add the views
            mailMessage.AlternateViews.Add(plainTextView);
            mailMessage.AlternateViews.Add(htmlView);

            return mailMessage;
        }

        private static string GetCurrentLanguage() {
            return LanguageScope.CurrentLanguage ?? String.Empty;
        }

        public void Dispose() {
            if (_templateService != null)
                _templateService.Dispose();
        }
    }
}