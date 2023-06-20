﻿using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public class NewToken : IOwnedByOrganizationAndProject
{
    public NewToken()
    {
        Scopes = new HashSet<string>();
    }

    public string OrganizationId { get; set; }
    public string ProjectId { get; set; }
    public string DefaultProjectId { get; set; }
    public HashSet<string> Scopes { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public string Notes { get; set; }
}
