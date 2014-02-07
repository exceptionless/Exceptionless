using System.Configuration;

namespace CodeSmith.Core.Configuration
{
    /// <summary>
    /// A class to define a Route configuration. This class cannot be inherited.
    /// </summary>
    public sealed class RouteElement : ConfigurationElement
    {
        private static readonly ConfigurationValidatorBase _nonEmptyStringValidator;
        private static readonly ConfigurationProperty _propConstraints;
        private static readonly ConfigurationProperty _propDataTokens;
        private static readonly ConfigurationProperty _propDefaults;
        private static readonly ConfigurationPropertyCollection _properties;
        private static readonly ConfigurationProperty _propHandlerType;

        private static readonly ConfigurationProperty _propName;
        private static readonly ConfigurationProperty _propUrl;

        /// <summary>
        /// Initializes the <see cref="RouteElement"/> class.
        /// </summary>
        static RouteElement()
        {
            _nonEmptyStringValidator = new StringValidator(1);

            _propName = new ConfigurationProperty(
                "name", typeof (string), null, null, _nonEmptyStringValidator,
                ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);

            _propUrl = new ConfigurationProperty(
                "url", typeof (string), null, null,
                _nonEmptyStringValidator,
                ConfigurationPropertyOptions.IsRequired);

            _propHandlerType = new ConfigurationProperty(
                "routeHandlerType", typeof (string),
                string.Empty, ConfigurationPropertyOptions.None);

            _propDefaults = new ConfigurationProperty(
                "defaults", typeof (RouteAttributeElement),
                null, ConfigurationPropertyOptions.None);

            _propConstraints = new ConfigurationProperty(
                "constraints", typeof (RouteAttributeElement),
                null, ConfigurationPropertyOptions.None);

            _propDataTokens = new ConfigurationProperty(
                "dataTokens", typeof (RouteAttributeElement),
                null, ConfigurationPropertyOptions.None);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_propName);
            _properties.Add(_propUrl);
            _properties.Add(_propHandlerType);
            _properties.Add(_propDefaults);
            _properties.Add(_propConstraints);
            _properties.Add(_propDataTokens);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteElement"/> class.
        /// </summary>
        public RouteElement()
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteElement"/> class.
        /// </summary>
        /// <param name="name">The name of the route.</param>
        public RouteElement(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteElement"/> class.
        /// </summary>
        /// <param name="name">The name of the route.</param>
        /// <param name="url">The URL pattern.</param>
        /// <param name="routeHandlerType">Type of the route handler.</param>
        public RouteElement(string name, string url, string routeHandlerType)
            : this(name)
        {
            Url = url;
            RouteHandlerType = routeHandlerType;
        }

        /// <summary>
        /// Gets or sets the name of the route.
        /// </summary>
        /// <value>The name of the route.</value>
        [StringValidator(MinLength = 1)]
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string) base["name"]; }
            set { base["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the URL pattern.
        /// </summary>
        /// <value>The URL pattern.</value>
        [StringValidator(MinLength = 1)]
        [ConfigurationProperty("url", IsRequired = true)]
        public string Url
        {
            get { return (string) base["url"]; }
            set { base["url"] = value; }
        }

        /// <summary>
        /// Gets or sets the type of the route handler.
        /// </summary>
        /// <value>The type of the route handler.</value>
        [ConfigurationProperty("routeHandlerType", IsRequired = false)]
        public string RouteHandlerType
        {
            get { return (string) base["routeHandlerType"]; }
            set { base["routeHandlerType"] = value; }
        }

        /// <summary>
        /// Gets or sets the route defaults.
        /// </summary>
        /// <value>The route defaults.</value>
        [ConfigurationProperty("defaults", IsRequired = false)]
        public RouteAttributeElement Defaults
        {
            get { return (RouteAttributeElement) base["defaults"]; }
            set { base["defaults"] = value; }
        }

        /// <summary>
        /// Gets or sets the route constraints.
        /// </summary>
        /// <value>The route constraints.</value>
        [ConfigurationProperty("constraints", IsRequired = false)]
        public RouteAttributeElement Constraints
        {
            get { return (RouteAttributeElement) base["constraints"]; }
            set { base["constraints"] = value; }
        }

        /// <summary>
        /// Gets or sets the route data tokens.
        /// </summary>
        /// <value>The route data tokens.</value>
        [ConfigurationProperty("dataTokens", IsRequired = false)]
        public RouteAttributeElement DataTokens
        {
            get { return (RouteAttributeElement) base["dataTokens"]; }
            set { base["dataTokens"] = value; }
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