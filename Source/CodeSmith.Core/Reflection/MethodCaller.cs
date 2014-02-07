using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// Provides methods to dynamically find and call methods.
    /// </summary>
    public static class MethodCaller
    {
        private const BindingFlags allLevelFlags = BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags ctorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags oneLevelFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags propertyFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        private static readonly Dictionary<MethodCacheKey, DynamicMemberHandle> _memberCache = new Dictionary<MethodCacheKey, DynamicMemberHandle>();
        private static readonly Dictionary<MethodCacheKey, DynamicMethodHandle> _methodCache = new Dictionary<MethodCacheKey, DynamicMethodHandle>();
        private static readonly Dictionary<Type, DynamicConstructor> _ctorCache = new Dictionary<Type, DynamicConstructor>();
        private static readonly Dictionary<Type, string[]> _propertyNameCache = new Dictionary<Type, string[]>();

        #region Dynamic Method Cache
        private static DynamicMethodHandle GetCachedMethod(object obj, MethodInfo info, params object[] parameters)
        {
            var key = new MethodCacheKey(obj.GetType().FullName, info.Name, GetParameterTypes(parameters));
            DynamicMethodHandle mh = null;
            if (_methodCache.TryGetValue(key, out mh))
                return mh;

            lock (_methodCache)
            {
                if (!_methodCache.TryGetValue(key, out mh))
                {
                    mh = new DynamicMethodHandle(info, parameters);
                    _methodCache.Add(key, mh);
                }
            }
            return mh;
        }

        private static DynamicMethodHandle GetCachedMethod(object obj, string method, params object[] parameters)
        {
            var key = new MethodCacheKey(obj.GetType().FullName, method, GetParameterTypes(parameters));
            DynamicMethodHandle mh = null;
            if (_methodCache.TryGetValue(key, out mh))
                return mh;

            lock (_methodCache)
            {
                if (!_methodCache.TryGetValue(key, out mh))
                {
                    MethodInfo info = GetMethod(obj.GetType(), method, parameters);
                    mh = new DynamicMethodHandle(info, parameters);
                    _methodCache.Add(key, mh);
                }
            }
            return mh;
        }
        #endregion

        #region Dynamic Constructor Cache
        private static DynamicConstructor GetCachedConstructor(Type objectType)
        {
            DynamicConstructor result = null;
            if (_ctorCache.TryGetValue(objectType, out result))
                return result;

            lock (_ctorCache)
            {
                if (!_ctorCache.TryGetValue(objectType, out result))
                {
                    ConstructorInfo info = objectType.GetConstructor(ctorFlags, null, Type.EmptyTypes, null);
                    result = DynamicMethodHandlerFactory.CreateConstructor(info);
                    _ctorCache.Add(objectType, result);
                }
            }
            return result;
        }
        #endregion

        #region Create Instance
        /// <summary>
        /// Uses reflection to create an object using its 
        /// default constructor.
        /// </summary>
        /// <param name="objectType">Type of object to create.</param>
        public static object CreateInstance(Type objectType)
        {
            DynamicConstructor ctor = GetCachedConstructor(objectType);
            if (ctor == null)
                throw new NotImplementedException("Default Constructor not implemented.");
            return ctor.Invoke();
        }

        #endregion

        internal static DynamicMemberHandle GetCachedProperty(Type objectType, string propertyName)
        {
            var key = new MethodCacheKey(objectType.FullName, propertyName, GetParameterTypes(null));
            DynamicMemberHandle mh = null;
            if (_memberCache.TryGetValue(key, out mh))
                return mh;

            lock (_memberCache)
            {
                if (!_memberCache.TryGetValue(key, out mh))
                {
                    PropertyInfo info = FindProperty(objectType, propertyName);
                    if (info != null)
                        mh = new DynamicMemberHandle(info);
                    _memberCache.Add(key, mh);
                }
            }
            return mh;
        }

        internal static DynamicMemberHandle GetCachedField(Type objectType, string fieldName)
        {
            var key = new MethodCacheKey(objectType.FullName, fieldName, GetParameterTypes(null));
            DynamicMemberHandle mh = null;
            if (_memberCache.TryGetValue(key, out mh))
                return mh;

            lock (_memberCache)
            {
                if (!_memberCache.TryGetValue(key, out mh))
                {
                    FieldInfo info = objectType.GetField(fieldName, fieldFlags);
                    if (info != null)
                        mh = new DynamicMemberHandle(info);
                    _memberCache.Add(key, mh);
                }
            }
            return mh;
        }

        internal static string[] GetCachedPropertyNames(Type objectType)
        {
            string[] names;
            if (_propertyNameCache.TryGetValue(objectType, out names))
                return names;

            lock (_propertyNameCache)
            {
                if (!_propertyNameCache.TryGetValue(objectType, out names))
                {
                    names = GetPropertyNames(objectType);
                    _propertyNameCache.Add(objectType, names);
                }
            }

            return names;
        }

        internal static string[] GetPropertyNames(Type objectType)
        {
#if SILVERLIGHT
            var props = objectType.GetProperties();
            return props.Select(prop => prop.Name).ToArray();
#else
            var result = new List<string>();
            var props = TypeDescriptor.GetProperties(objectType);
            
            foreach (PropertyDescriptor item in props)
                if (item.IsBrowsable)
                    result.Add(item.Name);

            string[] names = result.ToArray();
            return names;
#endif
        }

        /// <summary>
        /// Invokes a property getter using dynamic
        /// method invocation.
        /// </summary>
        /// <param name="obj">Target object.</param>
        /// <param name="property">Property to invoke.</param>
        /// <returns></returns>
        public static object CallPropertyGetter(object obj, string property)
        {
            DynamicMemberHandle mh = GetCachedProperty(obj.GetType(), property);
            return mh.DynamicMemberGet(obj);
        }

        /// <summary>
        /// Invokes a property setter using dynamic
        /// method invocation.
        /// </summary>
        /// <param name="obj">Target object.</param>
        /// <param name="property">Property to invoke.</param>
        /// <param name="value">New value for property.</param>
        public static void CallPropertySetter(object obj, string property, object value)
        {
            DynamicMemberHandle mh = GetCachedProperty(obj.GetType(), property);
            mh.DynamicMemberSet(obj, value);
        }

        /// <summary>
        /// Returns an array of Type objects corresponding
        /// to the type of parameters provided.
        /// </summary>
        /// <param name="parameters">
        /// Parameter values.
        /// </param>
        public static Type[] GetParameterTypes(object[] parameters)
        {
            var result = new List<Type>();

            if (parameters == null)
            {
                result.Add(typeof(object));
            }
            else
            {
                foreach (object item in parameters)
                    result.Add(item == null ? typeof(object) : item.GetType());
            }

            return result.ToArray();
        }

        #region Call Method

        /// <summary>
        /// Uses reflection to dynamically invoke a method
        /// if that method is implemented on the target object.
        /// </summary>
        /// <param name="obj">
        /// Object containing method.
        /// </param>
        /// <param name="method">
        /// Name of the method.
        /// </param>
        /// <param name="parameters">
        /// Parameters to pass to method.
        /// </param>
        public static object CallMethodIfImplemented(object obj, string method, params object[] parameters)
        {
            DynamicMethodHandle mh = GetCachedMethod(obj, method, parameters);
            if (mh == null || mh.DynamicMethod == null)
                return null;
            return CallMethod(obj, mh, parameters);
        }

        /// <summary>
        /// Uses reflection to dynamically invoke a method,
        /// throwing an exception if it is not
        /// implemented on the target object.
        /// </summary>
        /// <param name="obj">
        /// Object containing method.
        /// </param>
        /// <param name="method">
        /// Name of the method.
        /// </param>
        /// <param name="parameters">
        /// Parameters to pass to method.
        /// </param>
        public static object CallMethod(object obj, string method, params object[] parameters)
        {
            DynamicMethodHandle mh = GetCachedMethod(obj, method, parameters);
            if (mh == null || mh.DynamicMethod == null)
                throw new NotImplementedException(method + " not implemented.");
            return CallMethod(obj, mh, parameters);
        }

        /// <summary>
        /// Uses reflection to dynamically invoke a method,
        /// throwing an exception if it is not
        /// implemented on the target object.
        /// </summary>
        /// <param name="obj">
        /// Object containing method.
        /// </param>
        /// <param name="info">
        /// MethodInfo for the method.
        /// </param>
        /// <param name="parameters">
        /// Parameters to pass to method.
        /// </param>
        public static object CallMethod(object obj, MethodInfo info, params object[] parameters)
        {
            DynamicMethodHandle mh = GetCachedMethod(obj, info, parameters);
            if (mh == null || mh.DynamicMethod == null)
                throw new NotImplementedException(info.Name + " not implemented.");
            return CallMethod(obj, mh, parameters);
        }

        /// <summary>
        /// Uses reflection to dynamically invoke a method,
        /// throwing an exception if it is not implemented
        /// on the target object.
        /// </summary>
        /// <param name="obj">
        /// Object containing method.
        /// </param>
        /// <param name="methodHandle">
        /// MethodHandle for the method.
        /// </param>
        /// <param name="parameters">
        /// Parameters to pass to method.
        /// </param>
        private static object CallMethod(object obj, DynamicMethodHandle methodHandle, params object[] parameters)
        {
            object result = null;
            DynamicMemberMethod method = methodHandle.DynamicMethod;

            object[] inParams = null;
            if (parameters == null)
                inParams = new object[] { null };
            else
                inParams = parameters;

            if (methodHandle.HasFinalArrayParam)
            {
                int pCount = methodHandle.MethodParamsLength;
                // last param is a param array or only param is an array
                int extras = inParams.Length - (pCount - 1);

                // 1 or more params go in the param array
                // copy extras into an array
                object[] extraArray = GetExtrasArray(extras, methodHandle.FinalArrayElementType);
                Array.Copy(inParams, extraArray, extras);

                // copy items into new array
                var paramList = new object[pCount];
                for (int pos = 0; pos <= pCount - 2; pos++)
                    paramList[pos] = parameters[pos];
                paramList[paramList.Length - 1] = extraArray;

                // use new array
                inParams = paramList;
            }
            try
            {
                result = methodHandle.DynamicMethod(obj, inParams);
            }
            catch (Exception ex)
            {
                throw new CallMethodException(methodHandle.MethodName + " method call failed.", ex);
            }
            return result;
        }

        private static object[] GetExtrasArray(int count, Type arrayType)
        {
            return (object[])(Array.CreateInstance(arrayType.GetElementType(), count));
        }

        #endregion

        #region Get/Find Method
        /// <summary>
        /// Uses reflection to locate a matching method
        /// on the target object.
        /// </summary>
        /// <param name="objectType">
        /// Type of object containing method.
        /// </param>
        /// <param name="method">
        /// Name of the method.
        /// </param>
        /// <param name="parameters">
        /// Parameters to pass to method.
        /// </param>
        public static MethodInfo GetMethod(Type objectType, string method, params object[] parameters)
        {
            MethodInfo result = null;

            object[] inParams = null;
            inParams = parameters ?? new object[] { null };

            // try to find a strongly typed match

            // first see if there's a matching method
            // where all params match types
            result = FindMethod(objectType, method, GetParameterTypes(inParams));

            if (result == null)
                // no match found - so look for any method
                // with the right number of parameters
                try
                {
                    result = FindMethod(objectType, method, inParams.Length);
                }
                catch (AmbiguousMatchException)
                {
                    // we have multiple methods matching by name and parameter count
                    result = FindMethodUsingFuzzyMatching(objectType, method, inParams);
                }

            // no strongly typed match found, get default based on name only
            if (result == null)
                result = objectType.GetMethod(method, allLevelFlags);
            return result;
        }

        private static MethodInfo FindMethodUsingFuzzyMatching(Type objectType, string method, object[] parameters)
        {
            MethodInfo result = null;
            Type currentType = objectType;
            do
            {
                MethodInfo[] methods = currentType.GetMethods(oneLevelFlags);
                int parameterCount = parameters.Length;
                // Match based on name and parameter types and parameter arrays
                foreach (MethodInfo m in methods)
                {
                    if (m.Name != method)
                        continue;

                    ParameterInfo[] infoParams = m.GetParameters();
                    int pCount = infoParams.Length;
                    if (pCount > 0 &&
                        ((pCount == 1 && infoParams[0].ParameterType.IsArray) ||
                         (infoParams[pCount - 1].GetCustomAttributes(typeof(ParamArrayAttribute), true).Length > 0)))
                        // last param is a param array or only param is an array
                        if (parameterCount >= pCount - 1)
                        {
                            // got a match so use it
                            result = m;
                            break;
                        }
                }

                if (result == null)
                {
                    // match based on parameter name and number of parameters
                    foreach (MethodInfo m in methods)
                    {
                        if (m.Name != method || m.GetParameters().Length != parameterCount)
                            continue;
                        result = m;
                        break;
                    }
                }

                if (result != null)
                    break;
                currentType = currentType.BaseType;
            } while (currentType != null);


            return result;
        }

        /// <summary>
        /// Returns information about the specified
        /// method, even if the parameter types are
        /// generic and are located in an abstract
        /// generic base class.
        /// </summary>
        /// <param name="objectType">
        /// Type of object containing method.
        /// </param>
        /// <param name="method">
        /// Name of the method.
        /// </param>
        /// <param name="types">
        /// Parameter types to pass to method.
        /// </param>
        public static MethodInfo FindMethod(Type objectType, string method, Type[] types)
        {
            MethodInfo info = null;
            do
            {
                // find for a strongly typed match
                info = objectType.GetMethod(method, oneLevelFlags, null, types, null);
                if (info != null)
                    break; // match found

                objectType = objectType.BaseType;
            } while (objectType != null);

            return info;
        }

        /// <summary>
        /// Returns information about the specified
        /// method, finding the method based purely
        /// on the method name and number of parameters.
        /// </summary>
        /// <param name="objectType">
        /// Type of object containing method.
        /// </param>
        /// <param name="method">
        /// Name of the method.
        /// </param>
        /// <param name="parameterCount">
        /// Number of parameters to pass to method.
        /// </param>
        public static MethodInfo FindMethod(Type objectType, string method, int parameterCount)
        {
            // walk up the inheritance hierarchy looking
            // for a method with the right number of
            // parameters
            MethodInfo result = null;
            Type currentType = objectType;
            do
            {
                MethodInfo info = currentType.GetMethod(method, oneLevelFlags);
                if (info != null)
                {
                    ParameterInfo[] infoParams = info.GetParameters();
                    int pCount = infoParams.Length;
                    if (pCount > 0 &&
                        ((pCount == 1 && infoParams[0].ParameterType.IsArray) ||
                         (infoParams[pCount - 1].GetCustomAttributes(typeof(ParamArrayAttribute), true).Length > 0)))
                    {
                        // last param is a param array or only param is an array
                        if (parameterCount >= pCount - 1)
                        {
                            // got a match so use it
                            result = info;
                            break;
                        }
                    }
                    else if (pCount == parameterCount)
                    {
                        // got a match so use it
                        result = info;
                        break;
                    }
                }
                currentType = currentType.BaseType;
            } while (currentType != null);

            return result;
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
            if (type == null)
                throw new ArgumentNullException("type");
            if (propertyName == null)
                throw new ArgumentNullException("propertyName");

            // first try GetProperty
            PropertyInfo property = type.GetProperty(propertyName, propertyFlags);
            if (property != null)
                return property;

            // if not found, search while ignoring case
            foreach (PropertyInfo p in type.GetProperties())
                if (p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    return p;

            return null;
        }

        #endregion
    }
}