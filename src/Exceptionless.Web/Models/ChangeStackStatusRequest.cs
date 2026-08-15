using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models;

public sealed class ChangeStackStatusRequest
{
    private const string ChangeableStatusPattern = "(?i)^(" + Stack.KnownStatuses.Open + "|" + Stack.KnownStatuses.Fixed + "|" + Stack.KnownStatuses.Ignored + "|" + Stack.KnownStatuses.Discarded + ")$";

    [FromQuery(Name = "status")]
    [MinLength(1, ErrorMessage = "The status is invalid.")]
    [RegularExpression(ChangeableStatusPattern, ErrorMessage = "The status is invalid.")]
    public string? Status { get; set; }
}
