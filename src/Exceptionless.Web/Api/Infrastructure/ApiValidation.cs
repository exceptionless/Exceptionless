namespace Exceptionless.Web.Api.Infrastructure;

public static class ApiValidation
{
    public static IResult MissingRequestBody()
    {
        return global::Microsoft.AspNetCore.Http.Results.ValidationProblem(
            new Dictionary<string, string[]> { [String.Empty] = ["A non-empty request body is required."] },
            statusCode: StatusCodes.Status400BadRequest);
    }

}
