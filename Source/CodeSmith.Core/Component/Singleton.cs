
namespace CodeSmith.Core.Component
{
    /// <summary>
    /// Provides a global point of access to a single instance of a given class.
    /// </summary>
    /// <typeparam name="T">The type to provide a singleton instance for.</typeparam>
    /// <remarks>
    /// <para>
    /// The singleton instance can be accessed through a static property,
    /// where the type of the singleton is passed as a generic type parameter.
    /// Subsequent requests for the instance will yield the same class instance.
    /// </para>
    /// <para>
    /// The singleton is thread-safe and lazy (i.e. it is created when the instance is first requested).
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example gets a singleton instance of a <c>Foo</c> class:
    /// <code>
    /// Foo singleInstance = Singleton.Current;
    /// </code>
    /// </example>
    public static class Singleton<T> where T : new()
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static T Current
        {
            get
            {
                return Nested.Current;
            }
        }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit.
            static Nested()
            { }

            internal static readonly T Current = new T();
        }
    }
}
