using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for Token types.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class TokenMapper
{
    [MapperIgnoreTarget(nameof(Token.Type))]
    public partial Token MapToToken(NewToken source);

    public partial ViewToken MapToViewToken(Token source);

    public List<ViewToken> MapToViewTokens(IEnumerable<Token> source)
        => source.Select(MapToViewToken).ToList();
}
