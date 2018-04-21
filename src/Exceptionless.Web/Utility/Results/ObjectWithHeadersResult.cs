using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Api.Utility.Results {
    public class ObjectWithHeadersResult : ObjectResult {
        public ObjectWithHeadersResult(object value, IHeaderDictionary headers) : base(value) {
            Headers = headers ?? new HeaderDictionary();
        }

        public IHeaderDictionary Headers { get; set; }

        public override void OnFormatting(ActionContext context) {
            base.OnFormatting(context);

            if (Headers == null)
                return;

            foreach (var header in Headers)
                context.HttpContext.Response.Headers.Add(header.Key, header.Value);
        }
    }
}
