using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Exceptionless.Core.Reflection
{
    /// <summary>
    /// A class holding all the accessors for a <see cref="Type"/>.
    /// </summary>
    internal class TypeAccessor
    {
        private readonly ConcurrentDictionary<string, IMemberAccessor> _memberCache = new ConcurrentDictionary<string, IMemberAccessor>();
        private readonly Lazy<LateBoundConstructor> _lateBoundConstructor;
        private readonly Type _type;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeAccessor"/> class.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> this accessor is for.</param>
        public TypeAccessor(Type type)
        {
            _type = type;
            _lateBoundConstructor = new Lazy<LateBoundConstructor>(() => DelegateFactory.CreateConstructor(_type));
        }

        /// <summary>
        /// Gets the <see cref="Type"/> this accessor is for.
        /// </summary>
        /// <value>The <see cref="Type"/> this accessor is for.</value>
        public Type Type => _type;

        /// <summary>
        /// Creates a new instance of accessors type.
        /// </summary>
        /// <returns>A new instance of accessors type.</returns>
        public object Create()
        {
            var constructor = _lateBoundConstructor.Value;
            if (constructor == null)
                throw new InvalidOperationException($"Could not find constructor for '{Type.Name}'.");

            return constructor.Invoke();
        }

        #region FindProperty
        /// <summary>
        /// Searches for the public property with the specified name.
        /// </summary>
        /// <param name="name">The name of the property to find.</param>
        /// <returns>An <see cref="IMemberAccessor"/> instance for the property if found; otherwise <c>null</c>.</returns>
        public IMemberAccessor FindProperty(string name)
        {
            return FindProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        }

        /// <summary>
        /// Searches for the specified property, using the specified binding constraints.
        /// </summary>
        /// <param name="name">The name of the property to find.</param>
        /// <param name="flags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
        /// <returns>
        /// An <see cref="IMemberAccessor"/> instance for the property if found; otherwise <c>null</c>.
        /// </returns>
        public IMemberAccessor FindProperty(string name, BindingFlags flags)
        {
            return _memberCache.GetOrAdd(name, n => CreatePropertyAccessor(n, flags));
        }

        private IMemberAccessor CreatePropertyAccessor(string name, BindingFlags flags)
        {
            var info = FindProperty(Type, name, flags);
            return info == null ? null : GetMemberAccessor(info);
        }

        private static PropertyInfo FindProperty(Type type, string name, BindingFlags flags)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            // first try GetProperty
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property;

            // if not found, search while ignoring case
            return type.GetProperties(flags).FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static IMemberAccessor GetMemberAccessor(PropertyInfo propertyInfo)
        {
            return propertyInfo == null ? null : new PropertyAccessor(propertyInfo);
        }
        #endregion

        #region FindField
        /// <summary>
        /// Searches for the specified field with the specified name.
        /// </summary>
        /// <param name="name">The name of the field to find.</param>
        /// <returns>
        /// An <see cref="IMemberAccessor"/> instance for the field if found; otherwise <c>null</c>.
        /// </returns>
        public IMemberAccessor FindField(string name)
        {
            return FindField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// Searches for the specified field, using the specified binding constraints.
        /// </summary>
        /// <param name="name">The name of the field to find.</param>
        /// <param name="flags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
        /// <returns>
        /// An <see cref="IMemberAccessor"/> instance for the field if found; otherwise <c>null</c>.
        /// </returns>
        public IMemberAccessor FindField(string name, BindingFlags flags)
        {
            return _memberCache.GetOrAdd(name, n => CreateFieldAccessor(n, flags));
        }

        private IMemberAccessor CreateFieldAccessor(string name, BindingFlags flags)
        {
            var info = FindField(Type, name, flags);
            return info == null ? null : GetMemberAccessor(info);
        }

        private static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            // first try GetField
            var field = type.GetField(name, flags);
            if (field != null)
                return field;

            // if not found, search while ignoring case
            return type.GetFields(flags).FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static IMemberAccessor GetMemberAccessor(FieldInfo fieldInfo)
        {
            return fieldInfo == null ? null : new FieldAccessor(fieldInfo);
        }
        #endregion
    }
}