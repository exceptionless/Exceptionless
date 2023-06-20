﻿using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models;

public class ViewToken : IIdentity, IHaveDates
{
    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public string ProjectId { get; set; }
    public string UserId { get; set; }
    public string DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public string Notes { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
