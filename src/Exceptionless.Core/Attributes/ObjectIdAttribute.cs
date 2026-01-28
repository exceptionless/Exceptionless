using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Attributes;

/// <summary>
/// Marks a string property as a MongoDB ObjectId (24-char hex string).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ObjectIdAttribute : StringLengthAttribute
{
    public const string ObjectIdPattern = "^[a-fA-F0-9]{24}$";

    public ObjectIdAttribute() : base(24)
    {
        MinimumLength = 24;
    }
    public string Pattern => ObjectIdPattern;
}
