using System.Collections.Generic;
using System.Configuration;

namespace CodeSmith.Core.Configuration
{
    /// <summary>
    /// A class to define a Route attribute configuration. This class cannot be inherited.
    /// </summary>
    public sealed class RouteAttributeElement : ConfigurationElement
    {
        private static readonly ConfigurationPropertyCollection _properties = new ConfigurationPropertyCollection();

        private readonly Dictionary<string, object> _attributes = new Dictionary<string, object>();

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <value>The attributes.</value>
        public Dictionary<string, object> Attributes
        {
            get { return _attributes; }
        }

        /// <summary>
        /// Gets the collection of properties.
        /// </summary>
        /// <value></value>
        /// <returns>The <see cref="T:System.Configuration.ConfigurationPropertyCollection"/> of properties for the element.</returns>
        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        /// <summary>
        /// Gets a value indicating whether an unknown attribute is encountered during deserialization.
        /// </summary>
        /// <param name="name">The name of the unrecognized attribute.</param>
        /// <param name="value">The value of the unrecognized attribute.</param>
        /// <returns>
        /// true when an unknown attribute is encountered while deserializing; otherwise, false.
        /// </returns>
        protected override bool OnDeserializeUnrecognizedAttribute(string name, string value)
        {
            _attributes.Add(name, value);
            return true;
        }
    }
}