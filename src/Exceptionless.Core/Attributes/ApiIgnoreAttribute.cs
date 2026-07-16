namespace Exceptionless.Core.Attributes;

/// <summary>
/// Excludes a persisted model member from HTTP JSON contracts while retaining it for storage
/// serialization. Prefer dedicated API models when a larger contract is being designed.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ApiIgnoreAttribute : Attribute;
