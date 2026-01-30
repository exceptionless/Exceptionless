using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for Organization types.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class OrganizationMapper
{
    private readonly TimeProvider _timeProvider;

    public OrganizationMapper(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public partial Organization MapToOrganization(NewOrganization source);

    [MapperIgnoreTarget(nameof(ViewOrganization.IsOverMonthlyLimit))]
    private partial ViewOrganization MapToViewOrganizationCore(Organization source);

    public ViewOrganization MapToViewOrganization(Organization source)
    {
        var result = MapToViewOrganizationCore(source);
        result.IsOverMonthlyLimit = source.IsOverMonthlyLimit(_timeProvider);
        return result;
    }

    public List<ViewOrganization> MapToViewOrganizations(IEnumerable<Organization> source)
        => source.Select(MapToViewOrganization).ToList();
}
