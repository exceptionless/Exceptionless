using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models;

public record UpdateProductTourProgress
{
    [Range(1, Int32.MaxValue)]
    public int Version { get; init; }

    [Required]
    [EnumDataType(typeof(ProductTourStatus))]
    public ProductTourStatus? Status { get; init; }
}
