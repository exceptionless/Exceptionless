using System.Collections.Generic;

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// Settings class for the <see cref="ObjectCopier"/>.
    /// </summary>
    public class ObjectCopierSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectCopierSettings"/> class.
        /// </summary>
        public ObjectCopierSettings()
        {
            SuppressExceptions = false;
            UseDynamicCache = true;
            IgnoreList = new List<string>();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="ObjectCopier"/> will suppress exceptions when copying.
        /// </summary>
        /// <value><c>true</c> to suppress exceptions; otherwise, <c>false</c>.</value>
        public bool SuppressExceptions { get; set; }

        /// <summary>
        /// Gets or sets the list of property names to ignore when <see cref="ObjectCopier"/> is copying properties.
        /// </summary>
        /// <value>The ignore list.</value>
        public IList<string> IgnoreList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="ObjectCopier"/> will use dynamic cache.
        /// </summary>
        /// <value><c>true</c> to use dynamic cache; otherwise, <c>false</c>.</value>
        public bool UseDynamicCache { get; set; }
    }
}