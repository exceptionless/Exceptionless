using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for User types.
/// Uses RequiredMappingStrategy.Target so new ViewUser properties
/// produce compile warnings unless explicitly mapped or ignored.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserMapper
{
    [MapperIgnoreTarget(nameof(ViewUser.IsInvite))]
    public partial ViewUser MapToViewUser(User source);

    public partial List<ViewUser> MapToViewUsers(IEnumerable<User> source);
}
