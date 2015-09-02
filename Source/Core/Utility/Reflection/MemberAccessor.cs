using System;
using System.Reflection;

namespace Exceptionless.Core.Reflection
{
    /// <summary>
    /// A base class for member accessors.
    /// </summary>
    internal abstract class MemberAccessor : IMemberAccessor, IEquatable<IMemberAccessor>
    {
        /// <summary>
        /// Gets the type of the member.
        /// </summary>
        /// <value>The type of the member.</value>
        public abstract Type MemberType { get; }
        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <value>The member info.</value>
        public abstract MemberInfo MemberInfo { get; }
        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        public abstract string Name { get; }
        /// <summary>
        /// Gets a value indicating whether this member has getter.
        /// </summary>
        /// <value><c>true</c> if this member has getter; otherwise, <c>false</c>.</value>
        public abstract bool HasGetter { get; }
        /// <summary>
        /// Gets a value indicating whether this member has setter.
        /// </summary>
        /// <value><c>true</c> if this member has setter; otherwise, <c>false</c>.</value>
        public abstract bool HasSetter { get; }

        /// <summary>
        /// Returns the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be returned.</param>
        /// <returns>
        /// The member value for the instance parameter.
        /// </returns>
        public abstract object GetValue(object instance);
        /// <summary>
        /// Sets the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be set.</param>
        /// <param name="value">The new value for this member.</param>
        public abstract void SetValue(object instance, object value);

        /// <summary>
        /// Determines whether the specified <see cref="IMemberAccessor"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="IMemberAccessor"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="IMemberAccessor"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(IMemberAccessor other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return Equals(other.MemberInfo, MemberInfo);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != typeof(MemberAccessor))
                return false;

            return Equals((MemberAccessor)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return MemberInfo.GetHashCode();
        }
    }
}