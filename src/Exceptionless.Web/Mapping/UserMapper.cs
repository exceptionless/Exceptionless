using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for User types.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class UserMapper
{
    public partial ViewUser MapToViewUser(User source);

    public List<ViewUser> MapToViewUsers(IEnumerable<User> source)
        => source.Select(MapToViewUser).ToList();
}
