using System;
using System.Reflection;

namespace Exceptionless.Core.Reflection
{
    /// <summary>
    /// An interface for member accessors.
    /// </summary>
    public interface IMemberAccessor
    {
        /// <summary>
        /// Gets the type of the member.
        /// </summary>
        /// <value>The type of the member.</value>
        Type MemberType { get; }
        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <value>The member info.</value>
        MemberInfo MemberInfo { get; }
        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        string Name { get; }
        /// <summary>
        /// Gets a value indicating whether this member has getter.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this member has getter; otherwise, <c>false</c>.
        /// </value>
        bool HasGetter { get; }
        /// <summary>
        /// Gets a value indicating whether this member has setter.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this member has setter; otherwise, <c>false</c>.
        /// </value>
        bool HasSetter { get; }

        /// <summary>
        /// Returns the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be returned.</param>
        /// <returns>The member value for the instance parameter.</returns>
        object GetValue(object instance);

        /// <summary>
        /// Sets the value of the member.
        /// </summary>
        /// <param name="instance">The object whose member value will be set.</param>
        /// <param name="value">The new value for this member.</param>
        void SetValue(object instance, object value);
    }
}