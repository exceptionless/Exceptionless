using System.Configuration;

namespace CodeSmith.Core.Configuration
{
    /// <summary>
    /// Defines configuration settings to support the infrastructure for configuring 
    /// and managing Mvc Route details. This class cannot be inherited.
    /// </summary>
    public sealed class RouteTableSection : ConfigurationSection
    {
        private static readonly ConfigurationPropertyCollection _properties;
        private static readonly ConfigurationProperty _propRoutes;

        /// <summary>
        /// Initializes the <see cref="RouteTableSection"/> class.
        /// </summary>
        static RouteTableSection()
        {
            _propRoutes = new ConfigurationProperty("routes", typeof (RouteElementCollection), null,
                                                    ConfigurationPropertyOptions.None);
            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_propRoutes);
        }

        /// <summary>
        /// Gets the routes.
        /// </summary>
        /// <value>The routes.</value>
        [ConfigurationProperty("routes", IsDefaultCollection = false)]
        public RouteElementCollection Routes
        {
            get { return (RouteElementCollection) base["routes"]; }
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
    }
}