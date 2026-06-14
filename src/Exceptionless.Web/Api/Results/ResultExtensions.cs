using Foundatio.Mediator;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Results;

/// <summary>
/// Extension methods to convert Foundatio.Mediator Result types to ASP.NET IResult.
/// Used in endpoint lambdas after invoking handlers via the mediator.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result (non-generic) to an HTTP IResult through the registered mediator result mapper.
    /// </summary>
    public static IHttpResult ToHttpResult(this Result result, IMediatorResultMapper<IHttpResult> resultMapper)
    {
        return resultMapper.MapResult(result);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an HTTP IResult through the registered mediator result mapper.
    /// </summary>
    public static IHttpResult ToHttpResult<T>(this Result<T> result, IMediatorResultMapper<IHttpResult> resultMapper)
    {
        return resultMapper.MapResult(result);
    }
}
