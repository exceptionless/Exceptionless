using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models.Data;

public record ProductTourProgress
{
    public ProductTourStatus Status { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int Version { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourStatus
{
    [JsonStringEnumMemberName("completed")]
    [EnumMember(Value = "completed")]
    Completed,
    [JsonStringEnumMemberName("dismissed")]
    [EnumMember(Value = "dismissed")]
    Dismissed
}
