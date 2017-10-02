// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Utility {
    /// <summary>
    /// A class the tracks changes (i.e. the Delta) for a particular <typeparamref name="TEntityType" />.
    /// </summary>
    /// <typeparam name="TEntityType">TEntityType is the base type of entity this delta tracks changes for.</typeparam>
    public class Delta<TEntityType> : DynamicObject /*,  IDelta */ where TEntityType : class {
        // cache property accessors for this type and all its derived types.
        private static ConcurrentDictionary<Type, Dictionary<string, IMemberAccessor>> _propertyCache = new ConcurrentDictionary<Type, Dictionary<string, IMemberAccessor>>();

        private Dictionary<string, IMemberAccessor> _propertiesThatExist;
        private readonly Dictionary<string, object> _unknownProperties = new Dictionary<string, object>();
        private HashSet<string> _changedProperties;
        private TEntityType _entity;
        private Type _entityType;

        /// <summary>
        /// Initializes a new instance of <see cref="Delta{TEntityType}" />.
        /// </summary>
        public Delta() : this(typeof(TEntityType)) {}

        /// <summary>
        /// Initializes a new instance of <see cref="Delta{TEntityType}" />.
        /// </summary>
        /// <param name="entityType">
        ///     The derived entity type for which the changes would be tracked.
        ///     <paramref name="entityType" /> should be assignable to instances of <typeparamref name="TEntityType" />.
        /// </param>
        public Delta(Type entityType) {
            Initialize(entityType);
        }

        /// <summary>
        /// The actual type of the entity for which the changes are tracked.
        /// </summary>
        public Type EntityType => _entityType;

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
        /// <param name="target">The target entity to set the value on</param>
        /// <returns>True if successful</returns>
        public bool TrySetPropertyValue(string name, object value, TEntityType target = null) {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!_propertiesThatExist.ContainsKey(name))
                return false;

            IMemberAccessor cacheHit = _propertiesThatExist[name];

            if (value == null && !IsNullable(cacheHit.MemberType))
                return false;

            if (value != null) {
                if (value is JToken) {
                    try {
                        value = JsonConvert.DeserializeObject(value.ToString(), cacheHit.MemberType);
                    } catch (Exception) {
                        return false;
                    }
                } else {
                    bool isGuid = cacheHit.MemberType == typeof(Guid) && value is string;
                    bool isEnum = cacheHit.MemberType.IsEnum && value is Int64 && (long)value <= int.MaxValue;
                    bool isInt32 = cacheHit.MemberType == typeof(int) && value is Int64 && (long)value <= int.MaxValue;

                    if (!cacheHit.MemberType.IsPrimitive && !isGuid && !isEnum && !cacheHit.MemberType.IsInstanceOfType(value))
                        return false;

                    if (isGuid)
                        value = new Guid((string)value);
                    if (isInt32)
                        value = (int)(long)value;
                    if (isEnum)
                        value = Enum.Parse(cacheHit.MemberType, value.ToString());
                }
            }

            //.Setter.Invoke(_entity, new object[] { value });
            cacheHit.SetValue(_entity ?? target, value);
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
        /// <param name="target">The target entity to get the value from</param>
        /// <returns>True if the Property was found</returns>
        public bool TryGetPropertyValue(string name, out object value, TEntityType target = null) {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (_propertiesThatExist.ContainsKey(name)) {
                IMemberAccessor cacheHit = _propertiesThatExist[name];
                value = cacheHit.GetValue(target ?? _entity);
                return true;
            }

            value = null;
            return false;
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
                throw new ArgumentNullException(nameof(name));

            if (_propertiesThatExist.TryGetValue(name, out IMemberAccessor value)) {
                type = value.MemberType;
                return true;
            }

            type = null;
            return false;
        }

        /// <summary>
        /// A dictionary of values that were set on the delta that don't exist in TEntityType.
        /// </summary>
        public IDictionary<string, object> UnknownProperties => _unknownProperties;

        /// <summary>
        /// Overrides the DynamicObject TrySetMember method, so that only the properties
        /// of <see cref="EntityType" /> can be set.
        /// </summary>
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

            // add properties that don't exist to the unknown properties collect
            if (!_propertiesThatExist.ContainsKey(binder.Name)) {
                _unknownProperties[binder.Name] = value;
                return true;
            }

            return TrySetPropertyValue(binder.Name, value);
        }

        /// <summary>
        /// Overrides the DynamicObject TryGetMember method, so that only the properties
        /// of <see cref="EntityType" /> can be got.
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

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
        /// Returns the Properties that have been modified from their original values through this Delta as an
        /// enumeration of Property Names
        /// </summary>
        public IEnumerable<string> GetChangedPropertyNames(TEntityType original) {
            if (original == null)
                return _changedProperties;

            var changedPropertyNames = new HashSet<string>();

            foreach (var propertyName in _changedProperties) {
                if (!TryGetPropertyValue(propertyName, out object originalValue, original))
                    changedPropertyNames.Add(propertyName);

                if (!TryGetPropertyValue(propertyName, out object newValue))
                    continue;

                if (originalValue == null && newValue == null)
                    continue;

                if (newValue == null || !newValue.Equals(originalValue))
                    changedPropertyNames.Add(propertyName);
            }

            return changedPropertyNames;
        }

        /// <summary>
        /// Returns the Properties that have not been modified through this Delta as an
        /// enumeration of Property Names
        /// </summary>
        public IEnumerable<string> GetUnchangedPropertyNames() {
            return _propertiesThatExist.Keys.Except(GetChangedPropertyNames());
        }

        /// <summary>
        /// Copies any changed property values that match up from the underlying entity (accessible via <see cref="GetEntity()" />)
        /// to the <paramref name="target" /> entity.
        /// </summary>
        /// <param name="target">The target entity to be updated.</param>
        public void CopyChangedValues(object target) {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            Type targetType = target.GetType();
            if (!_propertyCache.ContainsKey(targetType))
                CachePropertyAccessors(targetType);

            IMemberAccessor[] propertiesToCopy = GetChangedPropertyNames().Select(s => _propertiesThatExist[s]).ToArray();

            foreach (IMemberAccessor sourceProperty in propertiesToCopy) {
                object value = sourceProperty.GetValue(_entity);
                if (!_propertyCache[targetType].TryGetValue(sourceProperty.Name, out IMemberAccessor targetAccessor))
                    continue;

                if (!targetAccessor.MemberType.IsInstanceOfType(value))
                    continue;
                
                targetAccessor.SetValue(target, value);
            }
        }

        /// <summary>
        /// Copies the unchanged property values from the underlying entity (accessible via <see cref="GetEntity()" />)
        /// to the <paramref name="target" /> entity.
        /// </summary>
        /// <param name="target">The entity to be updated.</param>
        public void CopyUnchangedValues(object target) {
            if (target == null)
                throw new ArgumentNullException(nameof(target));


            Type targetType = target.GetType();
            if (!_propertyCache.ContainsKey(targetType))
                CachePropertyAccessors(targetType);

            IMemberAccessor[] propertiesToCopy = GetUnchangedPropertyNames().Select(s => _propertiesThatExist[s]).ToArray();

            foreach (IMemberAccessor sourceProperty in propertiesToCopy) {
                object value = sourceProperty.GetValue(_entity);
                if (!_propertyCache[targetType].TryGetValue(sourceProperty.Name, out IMemberAccessor targetAccessor))
                    continue;

                if (!targetAccessor.MemberType.IsInstanceOfType(value))
                    continue;

                targetAccessor.SetValue(target, value);
            }
        }

        /// <summary>
        /// Overwrites the <paramref name="target" /> entity with the changes tracked by this Delta.
        /// <remarks>The semantics of this operation are equivalent to a HTTP PATCH operation, hence the name.</remarks>
        /// </summary>
        /// <param name="target">The entity to be updated.</param>
        public void Patch(object target) {
            CopyChangedValues(target);
        }

        /// <summary>
        /// Overwrites the <paramref name="target" /> entity with the values stored in this Delta.
        /// <remarks>The semantics of this operation are equivalent to a HTTP PUT operation, hence the name.</remarks>
        /// </summary>
        /// <param name="target">The entity to be updated.</param>
        public void Put(object target) {
            CopyChangedValues(target);
            CopyUnchangedValues(target);
        }

        private void Initialize(Type entityType) {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (!typeof(TEntityType).IsAssignableFrom(entityType))
                throw new InvalidOperationException("Delta Entity Type Not Assignable");

            _entity = Activator.CreateInstance(entityType) as TEntityType;
            _changedProperties = new HashSet<string>();
            _entityType = entityType;
            CachePropertyAccessors(entityType);
            _propertiesThatExist = _propertyCache[entityType];
        }

        private void CachePropertyAccessors(Type type) {
            _propertyCache.GetOrAdd(type, t => {
                var properties = t.GetProperties()
                    .Where(p => p.GetSetMethod() != null && p.GetGetMethod() != null)
                    .Select(LateBinder.GetPropertyAccessor).ToList();

                var items = new Dictionary<string, IMemberAccessor>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in properties) {
                    items[p.Name] = p;
                    items[p.Name.ToLowerUnderscoredWords()] = p;
                }

                return items;
            });
        }

        public static bool IsNullable(Type type) {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}