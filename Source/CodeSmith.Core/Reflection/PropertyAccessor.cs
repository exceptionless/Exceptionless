using System;
using System.Reflection;
#if PFX_LEGACY_3_5
using CodeSmith.Core.Threading;
#endif

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// An accessor class for <see cref="PropertyInfo"/>.
    /// </summary>
    internal class PropertyAccessor : MemberAccessor
    {
        private readonly PropertyInfo _propertyInfo;
        private readonly string _name;
        private readonly bool _hasGetter;
        private readonly bool _hasSetter;
        private readonly Type _memberType;
        private readonly Lazy<LateBoundGet> _lateBoundGet;
        private readonly Lazy<LateBoundSet> _lateBoundSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyAccessor"/> class.
        /// </summary>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> instance to use for this accessor.</param>
        public PropertyAccessor(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
            _name = _propertyInfo.Name;
            _memberType = _propertyInfo.PropertyType;

            _hasGetter = _propertyInfo.GetGetMethod(true) != null;
            if (_hasGetter)
                _lateBoundGet = new Lazy<LateBoundGet>(() => DelegateFactory.CreateGet(_propertyInfo));

            _hasSetter = propertyInfo.GetSetMethod(true) != null;
            if (_hasSetter)
                _lateBoundSet = new Lazy<LateBoundSet>(() => DelegateFactory.CreateSet(_propertyInfo));
        }

        /// <summary>
        /// Gets the type of the member.
        /// </summary>
        /// <value>The type of the member.</value>
        public override Type MemberType
        {
            get { return _memberType; }
        }

        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <value>The member info.</value>
        public override MemberInfo MemberInfo
        {
            get { return _propertyInfo; }
        }

        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        public override string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets a value indicating whether this member has getter.
        /// </summary>
        /// <value><c>true</c> if this member has getter; otherwise, <c>false</c>.</value>
        public override bool HasGetter
        {
            get { return _hasGetter; }
        }

        /// <summary>
        /// Gets a value indicating whether this member has setter.
        /// </summary>
        /// <value><c>true</c> if this member has setter; otherwise, <c>false</c>.</value>
        public override bool HasSetter
        {
            get { return _hasSetter; }
        }

        /// <summary>
        /// Returns the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be returned.</param>
        /// <returns>
        /// The member value for the instance parameter.
        /// </returns>
        public override object GetValue(object instance)
        {
            if (_lateBoundGet == null || !HasGetter)
                throw new InvalidOperationException(String.Format("Property '{0}' does not have a getter.", Name));

            var get = _lateBoundGet.Value;
            if (get == null)
                throw new InvalidOperationException(String.Format("Property '{0}' does not have a getter.", Name));

            return get(instance);
        }

        /// <summary>
        /// Sets the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be set.</param>
        /// <param name="value">The new value for this member.</param>
        public override void SetValue(object instance, object value)
        {
            if (_lateBoundSet == null || !HasSetter)
                throw new InvalidOperationException(String.Format("Property '{0}' does not have a setter.", Name));

            var set = _lateBoundSet.Value;
            if (set == null)
                throw new InvalidOperationException(String.Format("Property '{0}' does not have a setter.", Name));

            set(instance, value);
        }
    }
}