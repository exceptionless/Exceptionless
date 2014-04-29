// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;

namespace Exceptionless.Core.Web {
    /// <summary>
    /// A class the tracks changes (i.e. the Delta) for a particular <typeparamref name="TEntityType" />.
    /// </summary>
    /// <typeparam name="TEntityType">TEntityType is the base type of entity this delta tracks changes for.</typeparam>
    [NonValidatingParameterBinding]
    public class Delta<TEntityType> : DynamicObject /*,  IDelta */ where TEntityType : class {
        // cache property accessors for this type and all its derived types.
        private static ConcurrentDictionary<Type, Dictionary<string, PropertyAccessor<TEntityType>>> _propertyCache = new ConcurrentDictionary<Type, Dictionary<string, PropertyAccessor<TEntityType>>>();

        private Dictionary<string, PropertyAccessor<TEntityType>> _propertiesThatExist;
        private HashSet<string> _changedProperties;
        private TEntityType _entity;
        private Type _entityType;

        /// <summary>
        /// Initializes a new instance of <see cref="Delta{TEntityType}" />.
        /// </summary>
        public Delta()
            : this(typeof(TEntityType)) {}

        /// <summary>
        /// Initializes a new instance of <see cref="Delta{TEntityType}" />.
        /// </summary>
        /// <param name="entityType">
        /// The derived entity type for which the changes would be tracked.
        /// <paramref name="entityType" /> should be assignable to instances of <typeparamref name="TEntityType" />.
        /// </param>
        public Delta(Type entityType) {
            Initialize(entityType);
        }

        /// <summary>
        /// The actual type of the entity for which the changes are tracked.
        /// </summary>
        public Type EntityType { get { return _entityType; } }

        /// <summary>
        /// Clears the Delta and resets the underlying Entity.
        /// </summary>
        public void Clear() {
            Initialize(_entityType);
        }

        /// <summary>
        /// Attempts to set the Property called <paramref name="name" /> to the <paramref name="value" /> specified.
        /// <remarks>
        /// Only properties that exist on <see cref="EntityType" /> can be set.
        /// If there is a type mismatch the request will fail.
        /// </remarks>
        /// </summary>
        /// <param name="name">The name of the Property</param>
        /// <param name="value">The new value of the Property</param>
        /// <returns>True if successful</returns>
        public bool TrySetPropertyValue(string name, object value) {
            if (name == null)
                throw new ArgumentNullException("name");

            if (!_propertiesThatExist.ContainsKey(name))
                return false;

            PropertyAccessor<TEntityType> cacheHit = _propertiesThatExist[name];

            if (value == null && !IsNullable(cacheHit.Property.PropertyType))
                return false;

            if (value != null) {
                if (value is JToken) {
                    try {
                        value = JsonConvert.DeserializeObject(value.ToString(), cacheHit.Property.PropertyType);
                    } catch (Exception ex) {
                        Log.Error().Exception(ex).Message("Error deserializing value: {0}", value.ToString()).Report().Write();
                        return false;
                    }
                } else {
                    bool isGuid = cacheHit.Property.PropertyType == typeof(Guid) && value is string;
                    bool isEnum = cacheHit.Property.PropertyType.IsEnum && value is Int64 && (long)value <= int.MaxValue;
                    bool isInt32 = cacheHit.Property.PropertyType == typeof(int) && value is Int64 && (long)value <= int.MaxValue;

                    if (!cacheHit.Property.PropertyType.IsPrimitive && !isGuid && !isEnum && !cacheHit.Property.PropertyType.IsAssignableFrom(value.GetType()))
                        return false;

                    if (isGuid)
                        value = new Guid((string)value);
                    if (isInt32)
                        value = (int)(long)value;
                    if (isEnum)
                        value = Enum.Parse(cacheHit.Property.PropertyType, value.ToString());
                }
            }

            //.Setter.Invoke(_entity, new object[] { value });
            cacheHit.SetValue(_entity, value);
            _changedProperties.Add(name);
            return true;
        }

        /// <summary>
        /// Attempts to get the value of the Property called <paramref name="name" /> from the underlying Entity.
        /// <remarks>
        /// Only properties that exist on <see cref="EntityType" /> can be retrieved.
        /// Both modified and unmodified properties can be retrieved.
        /// </remarks>
        /// </summary>
        /// <param name="name">The name of the Property</param>
        /// <param name="value">The value of the Property</param>
        /// <returns>True if the Property was found</returns>
        public bool TryGetPropertyValue(string name, out object value) {
            if (name == null)
                throw new ArgumentNullException("name");

            if (_propertiesThatExist.ContainsKey(name)) {
                PropertyAccessor<TEntityType> cacheHit = _propertiesThatExist[name];
                value = cacheHit.GetValue(_entity);
                return true;
            } else {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to get the <see cref="Type" /> of the Property called <paramref name="name" /> from the underlying Entity.
        /// <remarks>
        /// Only properties that exist on <see cref="EntityType" /> can be retrieved.
        /// Both modified and unmodified properties can be retrieved.
        /// </remarks>
        /// </summary>
        /// <param name="name">The name of the Property</param>
        /// <param name="type">The type of the Property</param>
        /// <returns>Returns <c>true</c> if the Property was found and <c>false</c> if not.</returns>
        public bool TryGetPropertyType(string name, out Type type) {
            if (name == null)
                throw new ArgumentNullException("name");

            PropertyAccessor<TEntityType> value;
            if (_propertiesThatExist.TryGetValue(name, out value)) {
                type = value.Property.PropertyType;
                return true;
            } else {
                type = null;
                return false;
            }
        }

        /// <summary>
        /// Overrides the DynamicObject TrySetMember method, so that only the properties
        /// of <see cref="EntityType" /> can be set.
        /// </summary>
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            if (binder == null)
                throw new ArgumentNullException("binder");

            return TrySetPropertyValue(binder.Name, value);
        }

        /// <summary>
        /// Overrides the DynamicObject TryGetMember method, so that only the properties
        /// of <see cref="EntityType" /> can be got.
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            if (binder == null)
                throw new ArgumentNullException("binder");

            return TryGetPropertyValue(binder.Name, out result);
        }

        /// <summary>
        /// Returns the <see cref="EntityType" /> instance
        /// that holds all the changes (and original values) being tracked by this Delta.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Not appropriate to be a property")]
        public TEntityType GetEntity() {
            return _entity;
        }

        /// <summary>
        /// Returns the Properties that have been modified through this Delta as an
        /// enumeration of Property Names
        /// </summary>
        public IEnumerable<string> GetChangedPropertyNames() {
            return _changedProperties;
        }

        /// <summary>
        /// Returns the Properties that have not been modified through this Delta as an
        /// enumeration of Property Names
        /// </summary>
        public IEnumerable<string> GetUnchangedPropertyNames() {
            return _propertiesThatExist.Keys.Except(GetChangedPropertyNames());
        }

        /// <summary>
        /// Copies the changed property values from the underlying entity (accessible via <see cref="GetEntity()" />)
        /// to the <paramref name="original" /> entity.
        /// </summary>
        /// <param name="original">The entity to be updated.</param>
        public void CopyChangedValues(TEntityType original) {
            if (original == null)
                throw new ArgumentNullException("original");

            if (!_entityType.IsAssignableFrom(original.GetType()))
                throw new ArgumentException("Delta type mismatch", "original");

            PropertyAccessor<TEntityType>[] propertiesToCopy = GetChangedPropertyNames().Select(s => _propertiesThatExist[s]).ToArray();
            foreach (PropertyAccessor<TEntityType> propertyToCopy in propertiesToCopy)
                propertyToCopy.Copy(_entity, original);
        }

        /// <summary>
        /// Copies the unchanged property values from the underlying entity (accessible via <see cref="GetEntity()" />)
        /// to the <paramref name="original" /> entity.
        /// </summary>
        /// <param name="original">The entity to be updated.</param>
        public void CopyUnchangedValues(TEntityType original) {
            if (original == null)
                throw new ArgumentNullException("original");

            if (!_entityType.IsAssignableFrom(original.GetType()))
                throw new ArgumentException("Delta type mismatch", "original");

            PropertyAccessor<TEntityType>[] propertiesToCopy = GetUnchangedPropertyNames().Select(s => _propertiesThatExist[s]).ToArray();
            foreach (PropertyAccessor<TEntityType> propertyToCopy in propertiesToCopy)
                propertyToCopy.Copy(_entity, original);
        }

        /// <summary>
        /// Overwrites the <paramref name="original" /> entity with the changes tracked by this Delta.
        /// <remarks>The semantics of this operation are equivalent to a HTTP PATCH operation, hence the name.</remarks>
        /// </summary>
        /// <param name="original">The entity to be updated.</param>
        public void Patch(TEntityType original) {
            CopyChangedValues(original);
        }

        /// <summary>
        /// Overwrites the <paramref name="original" /> entity with the values stored in this Delta.
        /// <remarks>The semantics of this operation are equivalent to a HTTP PUT operation, hence the name.</remarks>
        /// </summary>
        /// <param name="original">The entity to be updated.</param>
        public void Put(TEntityType original) {
            CopyChangedValues(original);
            CopyUnchangedValues(original);
        }

        private void Initialize(Type entityType) {
            if (entityType == null)
                throw new ArgumentNullException("entityType");

            if (!typeof(TEntityType).IsAssignableFrom(entityType))
                throw new InvalidOperationException("Delta Entity Type Not Assignable");

            _entity = Activator.CreateInstance(entityType) as TEntityType;
            _changedProperties = new HashSet<string>();
            _entityType = entityType;
            _propertiesThatExist = InitializePropertiesThatExist();
        }

        private Dictionary<string, PropertyAccessor<TEntityType>> InitializePropertiesThatExist() {
            return _propertyCache.GetOrAdd(
                                           _entityType,
                (backingType) => backingType
                    .GetProperties()
                    .Where(p => p.GetSetMethod() != null && p.GetGetMethod() != null)
                    .Select<PropertyInfo, PropertyAccessor<TEntityType>>(p => new CompiledPropertyAccessor<TEntityType>(p))
                    .ToDictionary(p => p.Property.Name));
        }

        public static bool IsNullable(Type type) {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}