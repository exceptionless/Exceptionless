using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Utility.Results;

public class ObjectWithHeadersResult : ObjectResult
{
    public ObjectWithHeadersResult(object? value, IHeaderDictionary? headers) : base(value)
    {
        Headers = headers ?? new HeaderDictionary();
    }

    public IHeaderDictionary Headers { get; set; }

    public override void OnFormatting(ActionContext context)
    {
        base.OnFormatting(context);

        foreach (var header in Headers)
            context.HttpContext.Response.Headers[header.Key] = header.Value;
    }
}
