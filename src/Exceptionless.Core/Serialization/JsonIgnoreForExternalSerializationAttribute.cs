namespace Exceptionless.Core.Serialization;

/// <summary>
/// Excludes an internal persistence property from HTTP API serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class JsonIgnoreForExternalSerializationAttribute : Attribute;
