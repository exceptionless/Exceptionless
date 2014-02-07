using System;
using System.Collections.Generic;
using System.Configuration;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A configuration element collection class for <see cref="JobElement"/>.
    /// </summary>
    public class JobElementCollection : ConfigurationElementCollection, IEnumerable<IJobConfiguration>
    {
        /// <summary>
        /// When overridden in a derived class, creates a new <see cref="T:System.Configuration.ConfigurationElement"/>.
        /// </summary>
        /// <returns>
        /// A new <see cref="T:System.Configuration.ConfigurationElement"/>.
        /// </returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new JobElement();
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
            var j = element as JobElement;

            if (j == null)
                throw new ArgumentException("The specified element is not of the correct type.");

            return j.Name;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        IEnumerator<IJobConfiguration> IEnumerable<IJobConfiguration>.GetEnumerator()
        {
            foreach (IJobConfiguration configuration in this)
                yield return configuration;
        }
    }
}