using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public sealed class SystemSettings : IIdentity, IHaveDates
{
    public const string DefaultId = "000000000000000000000001";

    [ObjectId]
    public string Id { get; set; } = DefaultId;

    [MaxLength(200)]
    public string? AssistantModel { get; set; }

    [ObjectId]
    public string CreatedByUserId { get; set; } = null!;

    [ObjectId]
    public string UpdatedByUserId { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
