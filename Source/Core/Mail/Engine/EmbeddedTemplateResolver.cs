using System;
using System.IO;
using System.Reflection;
using RazorEngine.Templating;
#pragma warning disable 618

namespace RazorSharpEmail {
    /// <summary>
    /// Resolves templates embedded as resources in a target assembly.
    /// </summary>
    public class EmbeddedTemplateResolver : ITemplateResolver {
        private readonly Assembly _assembly;
        private readonly string _templateNamespace;

        /// <summary>
        /// Specify an assembly and the template namespace manually.
        /// </summary>
        /// <param name="assembly">The assembly where the templates are embedded.</param>
        /// <param name="templateNamespace"></param>
        public EmbeddedTemplateResolver(Assembly assembly, string templateNamespace) {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            if (templateNamespace == null)
                throw new ArgumentNullException("templateNamespace");

            _assembly = assembly;
            _templateNamespace = templateNamespace;
        }

        /// <summary>
        /// Uses a type reference to resolve the assembly and namespace where the template resources are embedded.
        /// </summary>
        /// <param name="type">The type whose namespace is used to scope the manifest resource name.</param>
        public EmbeddedTemplateResolver(Type type) {
            if (type == null)
                throw new ArgumentNullException("type");

            _assembly = Assembly.GetAssembly(type);
            _templateNamespace = type.Namespace;
        }

        public string Resolve(string name) {
            if (name == null)
                throw new ArgumentNullException("name");

            name = name.Replace('\\', '.').Replace('-', '_');

            Stream stream = _assembly.GetManifestResourceStream("{0}.{1}".FormatWith(_templateNamespace, name));

            if (stream == null)
                throw new ArgumentException("EmbeddedResourceNotFound");

            string template = ReadToEnd(stream);
            return template;
        }

        private string ReadToEnd(Stream stream) {
            var buffer = new byte[32768];

            var ms = new MemoryStream();
            while (true) {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                ms.Write(buffer, 0, read);
            }

            ms.Close();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
