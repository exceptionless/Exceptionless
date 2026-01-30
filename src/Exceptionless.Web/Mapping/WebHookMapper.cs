using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for WebHook types.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class WebHookMapper
{
    public partial WebHook MapToWebHook(NewWebHook source);
}
