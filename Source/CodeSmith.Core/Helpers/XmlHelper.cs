using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml;
using System.IO;
using CodeSmith.Core.IO;

namespace CodeSmith.Core.Helpers
{
    public class XmlHelper
    {
        public static IDictionary<string, string> GetNamespaces(string xml)
        {
            StringReader sr = new StringReader(xml);
            using (XmlReader reader = XmlReader.Create(sr))
            {
                XElement e = XElement.Load(reader);
                XPathNavigator nav = e.CreateNavigator();

                nav.MoveToFirstChild();
                IDictionary<string, string> namespaces = nav.GetNamespacesInScope(XmlNamespaceScope.Local);

                return namespaces;
            }
        }

        public static string GetFirstNamespace(string xml)
        {
            IDictionary<string, string> ns = GetNamespaces(xml);
            if (ns == null)
                return String.Empty;

            foreach (KeyValuePair<string, string> map in ns)
                return map.Value;

            return String.Empty;
        }

        public static string FormatXml(string xml)
        {
            var e = XElement.Parse(xml);

            var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
            var sb = new StringBuilder();
             
            using (var sw = new StringEncodedWriter(Encoding.UTF8, sb))
            {
                var writer = XmlWriter.Create(sw, settings);
                e.WriteTo(writer);
                writer.Close();
            }
            
            return sb.ToString();
        }
    }
}
