using System;
using System.Reflection;
#if PFX_LEGACY_3_5
using CodeSmith.Core.Threading;
#endif

namespace Exceptionless.Core.Reflection
{
    /// <summary>
    /// An accessor class for <see cref="FieldInfo"/>.
    /// </summary>
    internal class FieldAccessor : MemberAccessor
    {
        private readonly FieldInfo _fieldInfo;
        private readonly string _name;
        private readonly bool _hasGetter;
        private readonly bool _hasSetter;
        private readonly Type _memberType;
        private readonly Lazy<LateBoundGet> _lateBoundGet;
        private readonly Lazy<LateBoundSet> _lateBoundSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldAccessor"/> class.
        /// </summary>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance to use for this accessor.</param>
        public FieldAccessor(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
            _name = fieldInfo.Name;
            _memberType = fieldInfo.FieldType;

            _hasGetter = true;
            _lateBoundGet = new Lazy<LateBoundGet>(() => DelegateFactory.CreateGet(_fieldInfo));

            _hasSetter = !fieldInfo.IsInitOnly && !fieldInfo.IsLiteral;
            if (_hasSetter)
                _lateBoundSet = new Lazy<LateBoundSet>(() => DelegateFactory.CreateSet(_fieldInfo));
        }

        /// <summary>
        /// Gets the type of the member.
        /// </summary>
        /// <value>The type of the member.</value>
        public override Type MemberType => _memberType;

        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <value>The member info.</value>
        public override MemberInfo MemberInfo => _fieldInfo;

        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        public override string Name => _name;

        /// <summary>
        /// Gets a value indicating whether this member has getter.
        /// </summary>
        /// <value><c>true</c> if this member has getter; otherwise, <c>false</c>.</value>
        public override bool HasGetter => _hasGetter;

        /// <summary>
        /// Gets a value indicating whether this member has setter.
        /// </summary>
        /// <value><c>true</c> if this member has setter; otherwise, <c>false</c>.</value>
        public override bool HasSetter => _hasSetter;

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
                throw new InvalidOperationException($"Field '{Name}' does not have a getter.");

            var get = _lateBoundGet.Value;
            if (get == null)
                throw new InvalidOperationException($"Field '{Name}' does not have a getter.");

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
                throw new InvalidOperationException($"Field '{Name}' does not have a setter.");

            var set = _lateBoundSet.Value;
            if (set == null)
                throw new InvalidOperationException($"Field '{Name}' does not have a setter.");

            set(instance, value);
        }
    }
}