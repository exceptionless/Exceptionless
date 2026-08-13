using Exceptionless.Core.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models;

public sealed class ChangeStackStatusRequest
{
    [FromQuery(Name = "status")]
    [StackStatus]
    public string? Status { get; set; }
}
