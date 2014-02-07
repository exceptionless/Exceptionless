using System.Collections.ObjectModel;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A keyed collection of Jobs.
    /// </summary>
    public class JobCollection : KeyedCollection<string, Job>
    {
        /// <summary>
        /// When implemented in a derived class, extracts the key from the specified element.
        /// </summary>
        /// <param name="item">The element from which to extract the key.</param>
        /// <returns>The key for the specified element.</returns>
        protected override string GetKeyForItem(Job item)
        {
            return item.Name;
        }
    }
}