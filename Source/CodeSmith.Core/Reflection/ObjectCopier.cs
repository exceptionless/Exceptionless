using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

#if !SILVERLIGHT
using System.Data.Linq;
using System.Runtime.Serialization.Formatters.Binary;
#endif

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// Copy data from a source into a target object by copying public property values.
    /// </summary>
    /// <remarks></remarks>
    public static class ObjectCopier
    {
        private static readonly Type StringType = typeof(string);
        private static readonly Type ByteArrayType = typeof(byte[]);
        private static readonly Type NullableType = typeof(Nullable<>);
#if !SILVERLIGHT
        private static readonly Type BinaryType = typeof(Binary);
#endif
        #region Copy Object To Object
        /// <summary>
        /// Copies values from the source into the properties of the target.
        /// </summary>
        /// <param name="source">An object containing the source values.</param>
        /// <param name="target">An object with properties to be set from the source.</param>
        /// <remarks>
        /// The property names and types of the source object must match the property names and types
        /// on the target object. Source properties may not be indexed. 
        /// Target properties may not be readonly or indexed.
        /// </remarks>
        public static void Copy(object source, object target)
        {
            var settings = new ObjectCopierSettings();
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the properties of the target.
        /// </summary>
        /// <param name="source">An object containing the source values.</param>
        /// <param name="target">An object with properties to be set from the source.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        /// <remarks>
        /// The property names and types of the source object must match the property names and types
        /// on the target object. Source properties may not be indexed. 
        /// Target properties may not be readonly or indexed.
        /// </remarks>
        public static void Copy(object source, object target, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the properties of the target.
        /// </summary>
        /// <param name="source">An object containing the source values.</param>
        /// <param name="target">An object with properties to be set from the source.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        /// <param name="suppressExceptions">If <see langword="true" />, any exceptions will be suppressed.</param>
        /// <remarks>
        /// <para>
        /// The property names and types of the source object must match the property names and types
        /// on the target object. Source properties may not be indexed. 
        /// Target properties may not be readonly or indexed.
        /// </para><para>
        /// Properties to copy are determined based on the source object. Any properties
        /// on the source object marked with the <see cref="BrowsableAttribute"/> equal
        /// to false are ignored.
        /// </para>
        /// </remarks>
        public static void Copy(object source, object target, bool suppressExceptions, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                SuppressExceptions = suppressExceptions,
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the properties of the target.
        /// </summary>
        /// <param name="source">An object containing the source values.</param>
        /// <param name="target">An object with properties to be set from the source.</param>
        /// <param name="settings">The settings to use when copying properties.</param>
        /// <remarks>
        /// 	<para>
        /// The property names and types of the source object must match the property names and types
        /// on the target object. Source properties may not be indexed.
        /// Target properties may not be readonly or indexed.
        /// </para><para>
        /// Properties to copy are determined based on the source object. Any properties
        /// on the source object marked with the <see cref="BrowsableAttribute"/> equal
        /// to false are ignored.
        /// </para>
        /// </remarks>
        public static void Copy(object source, object target, ObjectCopierSettings settings)
        {
            if (source == null)
                throw new ArgumentNullException("source", "Source object can not be Null.");
            if (target == null)
                throw new ArgumentNullException("target", "Target object can not be Null.");

            if (settings == null)
                settings = new ObjectCopierSettings();

            string[] sourceProperties;
            if (settings.UseDynamicCache)
                sourceProperties = MethodCaller.GetCachedPropertyNames(source.GetType());
            else
                sourceProperties = MethodCaller.GetPropertyNames(source.GetType());

            foreach (string propertyName in sourceProperties)
            {
                if (settings.IgnoreList.Contains(propertyName))
                    continue;

                try
                {
                    object value = GetPropertyValue(source, propertyName, settings.UseDynamicCache);
                    SetPropertyValue(target, propertyName, value, settings.UseDynamicCache);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(String.Format("Property '{0}' copy failed.", propertyName));
                    if (!settings.SuppressExceptions)
                        throw new InvalidOperationException(
                            String.Format("Property '{0}' copy failed.", propertyName), ex);
                }
            }
        }
        #endregion

        #region Copy Object to IDictionary<string, object>
        /// <summary>
        /// Copies values from the source into the target <see cref="IDictionary"/>.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target <see cref="IDictionary"/>.</param>
        public static void Copy(object source, IDictionary<string, object> target)
        {
            var settings = new ObjectCopierSettings();
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the target <see cref="IDictionary"/>.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target <see cref="IDictionary"/>.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be added to the targeted <see cref="IDictionary"/>.</param>
        public static void Copy(object source, IDictionary<string, object> target, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the target <see cref="IDictionary"/>.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target <see cref="IDictionary"/>.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be added to the targeted <see cref="IDictionary"/>.</param>
        /// <param name="suppressExceptions">If <see langword="true" />, any exceptions will be suppressed.</param>
        public static void Copy(object source, IDictionary<string, object> target, bool suppressExceptions, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                SuppressExceptions = suppressExceptions,
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the source into the target <see cref="IDictionary"/>.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target <see cref="IDictionary"/>.</param>
        /// <param name="settings">The settings to use when copying properties.</param>
        public static void Copy(object source, IDictionary<string, object> target, ObjectCopierSettings settings)
        {
            if (source == null)
                throw new ArgumentNullException("source", "Source object can not be Null.");
            if (target == null)
                throw new ArgumentNullException("target", "Target object can not be Null.");
            if (settings == null)
                settings = new ObjectCopierSettings();

            string[] sourceProperties;
            if (settings.UseDynamicCache)
                sourceProperties = MethodCaller.GetCachedPropertyNames(source.GetType());
            else
                sourceProperties = MethodCaller.GetPropertyNames(source.GetType());

            foreach (string propertyName in sourceProperties)
            {
                if (settings.IgnoreList.Contains(propertyName))
                    continue;

                try
                {
                    object value = GetPropertyValue(source, propertyName, settings.UseDynamicCache);
                    target.Add(propertyName, value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(String.Format("Property '{0}' copy failed.", propertyName));
                    if (!settings.SuppressExceptions)
                        throw new ArgumentException(
                            String.Format("Property '{0}' copy failed.", propertyName), ex);
                }
            }
        }
        #endregion

#if !SILVERLIGHT
        #region Copy From NameValueCollection to Object
        /// <summary>
        /// Copies values from the <see cref="NameValueCollection"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="NameValueCollection"/> source.</param>
        /// <param name="target">The target object.</param>
        public static void Copy(NameValueCollection source, object target)
        {
            var settings = new ObjectCopierSettings();
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="NameValueCollection"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="NameValueCollection"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        public static void Copy(NameValueCollection source, object target, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="NameValueCollection"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="NameValueCollection"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        /// <param name="suppressExceptions">If <see langword="true" />, any exceptions will be suppressed.</param>
        public static void Copy(NameValueCollection source, object target, bool suppressExceptions, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                SuppressExceptions = suppressExceptions,
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="NameValueCollection"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="NameValueCollection"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="settings">The settings to use when copying properties.</param>
        public static void Copy(NameValueCollection source, object target, ObjectCopierSettings settings)
        {
            var newSource = new Dictionary<string, object>();
            for (int i = 0; i < source.Count; i++)
                if (!String.IsNullOrEmpty(source.Keys[i]))
                    newSource.Add(source.Keys[i], source[i]);

            Copy(newSource, target, settings);
        }
        #endregion
#endif

        #region Copy IDictionary<string, object> to Object
        /// <summary>
        /// Copies values from the <see cref="IDictionary"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="IDictionary"/> source.</param>
        /// <param name="target">The target object.</param>
        public static void Copy(IDictionary<string, object> source, object target)
        {
            var settings = new ObjectCopierSettings();
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="IDictionary"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="IDictionary"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        public static void Copy(IDictionary<string, object> source, object target, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="IDictionary"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="IDictionary"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="ignoreList">A list of property names to ignore. 
        /// These properties will not be set on the target object.</param>
        /// <param name="suppressExceptions">If <see langword="true" />, any exceptions will be suppressed.</param>
        public static void Copy(IDictionary<string, object> source, object target, bool suppressExceptions, params string[] ignoreList)
        {
            var settings = new ObjectCopierSettings
            {
                SuppressExceptions = suppressExceptions,
                IgnoreList = new List<string>(ignoreList),
            };
            Copy(source, target, settings);
        }

        /// <summary>
        /// Copies values from the <see cref="IDictionary"/> into the properties of the target.
        /// </summary>
        /// <param name="source">The <see cref="IDictionary"/> source.</param>
        /// <param name="target">The target object.</param>
        /// <param name="settings">The settings to use when copying properties.</param>
        public static void Copy(IDictionary<string, object> source, object target, ObjectCopierSettings settings)
        {
            if (source == null)
                throw new ArgumentNullException("source", "Source object can not be Null.");
            if (target == null)
                throw new ArgumentNullException("target", "Target object can not be Null.");
            if (settings == null)
                settings = new ObjectCopierSettings();

            foreach (var item in source)
            {
                if (settings.IgnoreList.Contains(item.Key))
                    continue;
                
                try
                {
                    SetPropertyValue(target, item.Key, item.Value, settings.UseDynamicCache);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(String.Format("Property '{0}' copy failed.", item.Key));
                    if (!settings.SuppressExceptions)
                        throw new ArgumentException(
                            String.Format("Property '{0}' copy failed.", item.Key), ex);
                }
            }
        }
        #endregion

#if !SILVERLIGHT
        /// <summary>
        /// Uses BinaryFormatter.Serialize to Clone an object.
        /// </summary>
        /// <param name="obj">The source object.</param>
        /// <returns>A cloned copy of the object.</returns>
        public static object BinaryClone(object obj)
        {
            using (var memStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.Clone));
                binaryFormatter.Serialize(memStream, obj);
                memStream.Seek(0, SeekOrigin.Begin);
                return binaryFormatter.Deserialize(memStream);
            }
        }
#endif
        /// <summary>
        /// Sets an object's property with the specified value,
        /// converting that value to the appropriate type if possible.
        /// </summary>
        /// <param name="target">Object containing the property to set.</param>
        /// <param name="propertyName">Name of the property to set.</param>
        /// <param name="value">Value to set into the property.</param>
        public static void SetPropertyValue(object target, string propertyName, object value)
        {
            SetPropertyValue(target, propertyName, value, true);
        }

        /// <summary>
        /// Sets an object's property with the specified value,
        /// converting that value to the appropriate type if possible.
        /// </summary>
        /// <param name="target">Object containing the property to set.</param>
        /// <param name="propertyName">Name of the property to set.</param>
        /// <param name="value">Value to set into the property.</param>
        /// <param name="useCache">if set to <c>true</c> use dynamic cache.</param>
        public static void SetPropertyValue(object target, string propertyName, object value, bool useCache)
        {
            if (target == null)
                throw new ArgumentNullException("target", "Target object can not be Null.");

            if (useCache)
            {
                DynamicMemberHandle handle = MethodCaller.GetCachedProperty(target.GetType(), propertyName);
                if (handle != null)
                    SetValueWithCoercion(target, handle, value);
            }
            else
            {
                PropertyInfo propertyInfo = MethodCaller.FindProperty(target.GetType(), propertyName);
                if (propertyInfo != null)
                    SetValueWithCoercion(target, propertyInfo, value);
            }
        }

        private static void SetValueWithCoercion(object target, DynamicMemberHandle handle, object value)
        {
            if (value == null)
                return;

            Type pType = handle.MemberType;
            Type vType = GetUnderlyingType(value.GetType());
            object v = CoerceValue(pType, vType, value);
            if (v != null)
                handle.DynamicMemberSet(target, v);
        }

        private static void SetValueWithCoercion(object target, PropertyInfo handle, object value)
        {
            if (value == null)
                return;

            Type pType = handle.PropertyType;
            Type vType = GetUnderlyingType(value.GetType());
            object v = CoerceValue(pType, vType, value);
            if (v != null)
                handle.SetValue(target, v, null);
        }

        /// <summary>
        /// Gets an object's property value by name.
        /// </summary>
        /// <param name="target">Object containing the property to get.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>The value of the property.</returns>
        public static object GetPropertyValue(object target, string propertyName)
        {
            return GetPropertyValue(target, propertyName, true);
        }

        /// <summary>
        /// Gets an object's property value by name.
        /// </summary>
        /// <param name="target">Object containing the property to get.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="useCache">if set to <c>true</c> use dynamic cache.</param>
        /// <returns>The value of the property.</returns>
        public static object GetPropertyValue(object target, string propertyName, bool useCache)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (String.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException("propertyName");

            if (useCache)
            {
                DynamicMemberHandle handle = MethodCaller.GetCachedProperty(target.GetType(), propertyName);
                return handle.DynamicMemberGet(target);
            }

            PropertyInfo propertyInfo = MethodCaller.FindProperty(target.GetType(), propertyName);
            return propertyInfo.GetValue(target, null);
        }

        /// <summary>
        /// Gets the underlying type dealing with <see cref="Nullable"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Returns a type dealing with <see cref="Nullable"/>.</returns>
        public static Type GetUnderlyingType(Type type)
        {
            Type t = type;
            bool isNullable = t.IsGenericType && (t.GetGenericTypeDefinition() == NullableType);
            if (isNullable)
                return Nullable.GetUnderlyingType(t);

            return t;
        }

        /// <summary>
        /// Attempts to coerce a value of one type into
        /// a value of a different type.
        /// </summary>
        /// <param name="desiredType">
        /// Type to which the value should be coerced.
        /// </param>
        /// <param name="valueType">
        /// Original type of the value.
        /// </param>
        /// <param name="value">
        /// The value to coerce.
        /// </param>
        /// <remarks>
        /// <para>
        /// If the desired type is a primitive type or Decimal, 
        /// empty string and null values will result in a 0 
        /// or equivalent.
        /// </para>
        /// <para>
        /// If the desired type is a <see cref="Nullable"/> type, empty string
        /// and null values will result in a null result.
        /// </para>
        /// <para>
        /// If the desired type is an <c>enum</c> the value's ToString()
        /// result is parsed to convert into the <c>enum</c> value.
        /// </para>
        /// </remarks>
        public static object CoerceValue(Type desiredType, Type valueType, object value)
        {
            // types match, just copy value
            if (desiredType.Equals(valueType))
                return value;

            bool isNullable = desiredType.IsGenericType && (desiredType.GetGenericTypeDefinition() == NullableType);
            if (isNullable)
            {
                if (value == null)
                    return null;
                if (valueType.Equals(StringType) && Convert.ToString(value) == String.Empty)
                    return null;
            }

            desiredType = GetUnderlyingType(desiredType);

            if ((desiredType.IsPrimitive || desiredType.Equals(typeof(decimal)))
                && valueType.Equals(StringType)
                && String.IsNullOrEmpty((string)value))
                return 0;

            if (value == null)
                return null;

            // types don't match, try to convert
            if (desiredType.Equals(typeof(Guid)))
                return new Guid(value.ToString());

            if (desiredType.IsEnum && valueType.Equals(StringType))
                return Enum.Parse(desiredType, value.ToString(), true);

#if !SILVERLIGHT
            bool isBinary = (desiredType.IsArray && desiredType.Equals(ByteArrayType)) || desiredType.Equals(BinaryType);

            if (isBinary && valueType.Equals(StringType))
            {
                byte[] bytes = Convert.FromBase64String((string)value);
                if (desiredType.IsArray && desiredType.Equals(ByteArrayType))
                    return bytes;

                return new Binary(bytes);
            }

            isBinary = (valueType.IsArray && valueType.Equals(ByteArrayType)) || valueType.Equals(BinaryType);

            if (isBinary && desiredType.Equals(StringType))
            {
                byte[] bytes = (value is Binary) ? ((Binary)value).ToArray() : (byte[])value;
                return Convert.ToBase64String(bytes);
            }
#endif
            try
            {
                if (desiredType.Equals(StringType))
                    return value.ToString();

                return Convert.ChangeType(value, desiredType, Thread.CurrentThread.CurrentCulture);
            }
            catch
            {
#if !SILVERLIGHT
                TypeConverter converter = TypeDescriptor.GetConverter(desiredType);
                if (converter != null && converter.CanConvertFrom(valueType))
                    return converter.ConvertFrom(value);
#endif
                throw;
            }
        }

        /// <summary>
        /// Finds a <see cref="PropertyInfo"/> by name ignoring case.
        /// </summary>
        /// <param name="type">The type to search.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>A <see cref="PropertyInfo"/> matching the property name.</returns>
        /// <remarks>
        /// FindProperty will first try to get a property matching the name and case of the 
        /// property name specified.  If a property cannot be found, all the properties will
        /// be searched ignoring the case of the name.
        /// </remarks>
        public static PropertyInfo FindProperty(Type type, string propertyName)
        {
            return MethodCaller.FindProperty(type, propertyName);
        }

    }
}
