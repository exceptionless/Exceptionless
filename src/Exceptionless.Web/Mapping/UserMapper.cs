using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for User types.
/// Uses RequiredMappingStrategy.Target so new ViewUser properties
/// produce compile warnings unless explicitly mapped or ignored.
/// Deep-copies collection properties (Roles, OrganizationIds) to prevent
/// controller-side mutations from affecting the source User model.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserMapper
{
    [MapperIgnoreTarget(nameof(ViewUser.IsInvite))]
    [MapperIgnoreTarget(nameof(ViewUser.Roles))]
    [MapperIgnoreTarget(nameof(ViewUser.OrganizationIds))]
    private partial ViewUser MapToViewUserCore(User source);

    public ViewUser MapToViewUser(User source)
    {
        var result = MapToViewUserCore(source);
        result = result with
        {
            Roles = new HashSet<string>(source.Roles),
            OrganizationIds = new HashSet<string>(source.OrganizationIds)
        };
        return result;
    }

    public List<ViewUser> MapToViewUsers(IEnumerable<User> source)
        => source.Select(MapToViewUser).ToList();
}
