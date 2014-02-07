using System;
using System.Configuration;
using System.Web.Routing;

namespace CodeSmith.Core.Configuration
{
    /// <summary>
    /// Represents a collection of <see cref="RouteElement"/> objects.
    /// </summary>
    [ConfigurationCollection(typeof (RouteElement))]
    public sealed class RouteElementCollection : ConfigurationElementCollection
    {
        private static readonly ConfigurationPropertyCollection _properties = new ConfigurationPropertyCollection();

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteCollection"/> class.
        /// </summary>
        public RouteElementCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {}

        /// <summary>
        /// Gets or sets the <see cref="RouteElement"/> at the specified index.
        /// </summary>
        /// <value></value>
        public RouteElement this[int index]
        {
            get { return (RouteElement) BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="RouteElement"/> with the specified key.
        /// </summary>
        /// <value></value>
        public new RouteElement this[string key]
        {
            get { return (RouteElement) BaseGet(key); }
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
        /// Adds the specified route.
        /// </summary>
        /// <param name="route">The route.</param>
        public void Add(RouteElement route)
        {
            BaseAdd(route);
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        public void Clear()
        {
            BaseClear();
        }

        /// <summary>
        /// Creates a new <see cref="T:System.Configuration.ConfigurationElement"/>.
        /// </summary>
        /// <returns>
        /// A new <see cref="T:System.Configuration.ConfigurationElement"/>.
        /// </returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new RouteElement();
        }

        /// <summary>
        /// Gets the element key for a specified configuration element when overridden in a derived class.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Configuration.ConfigurationElement"/> to return the key for.</param>
        /// <returns>
        /// An <see cref="T:System.Object"/> that acts as the key for the specified <see cref="T:System.Configuration.ConfigurationElement"/>.
        /// </returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((RouteElement) element).Name;
        }

        /// <summary>
        /// Removes the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        public void Remove(string name)
        {
            BaseRemove(name);
        }
    }
}