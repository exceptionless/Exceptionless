using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Exceptionless.Api.Utility.Results {
    public class OkPaginatedResult : ObjectWithHeadersResult {
        public OkPaginatedResult(object content, bool hasMore, int page, long? total = null, IHeaderDictionary headers = null) : base(content, headers) {
            StatusCode = StatusCodes.Status200OK;
            HasMore = hasMore;
            Page = page;
            Total = total;
        }

        public bool HasMore { get; set; }
        public int Page { get; set; }
        public long? Total { get; set; }

        public override void OnFormatting(ActionContext context) {
            AddPageLinkHeaders(context.HttpContext.Request);

            if (Total.HasValue)
                Headers.Add("X-Result-Count", Total.ToString());

            base.OnFormatting(context);
        }

        public void AddPageLinkHeaders(HttpRequest request) {
            bool includePrevious = Page > 1;
            bool includeNext = HasMore;

            if (!includePrevious && !includeNext)
                return;

            if (includePrevious) {
                var previousParameters = new Dictionary<string, StringValues>(request.Query) {
                    ["page"] = (Page - 1).ToString()
                };
                Headers.Add("Link", String.Concat("<", request.Path, "?", String.Join('&', previousParameters.Values), ">; rel=\"previous\""));
            }

            if (includeNext) {
                var nextParameters = new Dictionary<string, StringValues>(request.Query) {
                    ["page"] = (Page + 1).ToString()
                };

                Headers.Add("Link", String.Concat("<", request.Path, "?", String.Join('&', nextParameters.Values), ">; rel=\"next\""));
            }
        }
    }
}