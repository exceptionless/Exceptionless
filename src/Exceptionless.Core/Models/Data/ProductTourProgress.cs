using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models.Data;

public record ProductTourProgress
{
    public int Version { get; set; }
    public ProductTourStatus Status { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourStatus
{
    [JsonStringEnumMemberName("dismissed")]
    [EnumMember(Value = "dismissed")]
    Dismissed,
    [JsonStringEnumMemberName("completed")]
    [EnumMember(Value = "completed")]
    Completed
}
