using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace CodeSmith.Core.Xml
{
    /// <summary>
    /// A class to parse all the xml namespaces from an XPathNavigator.
    /// </summary>
    public class NamespaceParser
    {
        public enum DefaultPrefixScheme
        {
            AutomaticLetter = 1,
            PrefixPlusIndex = 2,
            PrefixAndLetter = 3
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamespaceParser"/> class.
        /// </summary>
        public NamespaceParser()
        {
            DefaultPrefix = "d";
            DefaultScheme = DefaultPrefixScheme.PrefixPlusIndex;
            ParseChildren = true;
            DefaultNamespaces = new HashSet<string>();
        }

        public ICollection<string> DefaultNamespaces { get; private set; }

        /// <summary>
        /// Gets or sets the namespaces collection.
        /// </summary>
        /// <value>The namespaces collection.</value>
        public IDictionary<string, string> Namespaces { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to parse child nodes to find namespaces.
        /// </summary>
        /// <value><c>true</c> if parse child nodes; otherwise, to only parse root element, <c>false</c>.</value>
        public bool ParseChildren { get; set; }

        /// <summary>
        /// Gets or sets the scheme to use when generating prefix values for default namespaces.
        /// </summary>
        /// <value>The default scheme.</value>
        public DefaultPrefixScheme DefaultScheme { get; set; }

        /// <summary>
        /// Gets or sets the default prefix.
        /// </summary>
        /// <value>The default prefix.</value>
        public string DefaultPrefix { get; set; }

        /// <summary>
        /// Parses an XML document for its namespaces.
        /// </summary>
        /// <param name="navigator">The navigator.</param>
        public void ParseNamespaces(XPathNavigator navigator)
        {
            if (navigator == null)
                throw new ArgumentNullException("navigator");

            if (Namespaces == null)
                Namespaces = new Dictionary<string, string>();

            if (String.IsNullOrEmpty(DefaultPrefix))
                DefaultPrefix = "d";

            DefaultNamespaces.Clear();

            navigator.MoveToRoot();
            RrecursiveParse(navigator);

            //add default namespaces
            int defaultIndex = 0;
            foreach (string name in DefaultNamespaces)
            {
                string key = GetDefaultKey(defaultIndex++);
                Namespaces.Add(key, name);
            }

        }

        private void RrecursiveParse(XPathNavigator navigator)
        {
            var namespaces = navigator.GetNamespacesInScope(XmlNamespaceScope.Local);
            foreach (var map in namespaces)
            {
                if (String.IsNullOrEmpty(map.Key))
                    DefaultNamespaces.Add(map.Value);
                else if (!Namespaces.ContainsKey(map.Key))
                    Namespaces.Add(map.Key, map.Value);
            }

            // process child element nodes
            if (navigator.HasChildren
                && (ParseChildren || navigator.NodeType == XPathNodeType.Root)
                && navigator.MoveToFirstChild())
            {
                do
                {
                    RrecursiveParse(navigator);
                }
                while (navigator.MoveToNext(XPathNodeType.Element));

                // move back to the original parent node
                navigator.MoveToParent();
            }
        }

        private string GetDefaultKey(int index)
        {
            int charStart = 97;
            int charEnd = 122;
            int charKey = charStart + index;

            bool isAutomaticLetter = DefaultScheme == DefaultPrefixScheme.AutomaticLetter;
            bool isPrefixAndLetter = DefaultScheme == DefaultPrefixScheme.PrefixAndLetter;

            string key = DefaultPrefix;

            do
            {
                // if run out of letters
                bool isCharEnd = charKey > charEnd;

                if (!isCharEnd && isAutomaticLetter)
                {
                    //letter at index
                    key = ((char)charKey).ToString();
                }
                else if (!isCharEnd && isPrefixAndLetter)
                {
                    //default and letter at index
                    key = DefaultPrefix + ((char)charKey).ToString();
                }
                else if (index > 0)
                {
                    key = DefaultPrefix + index.ToString();
                }
                index++;
                charKey++;
            }
            while (Namespaces.ContainsKey(key));

            return key;
        }

    }
}

