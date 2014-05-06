using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using CodeSmith.Core.Extensions;
using Exceptionless.Api.Controllers;
using Exceptionless.Models;

namespace Exceptionless.Api.Utility {
    public class OkWithHeadersContentResult<T> : OkNegotiatedContentResult<T> {
        public OkWithHeadersContentResult(T content, IContentNegotiator contentNegotiator, HttpRequestMessage request, IEnumerable<MediaTypeFormatter> formatters) : base(content, contentNegotiator, request, formatters) { }

        public OkWithHeadersContentResult(T content, ApiController controller, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = null)
            : base(content, controller) {
            Headers = headers;
        }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }

        public async override Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken) {
            HttpResponseMessage response = await base.ExecuteAsync(cancellationToken);

            if (Headers != null)
                foreach (var header in Headers)
                    response.Headers.Add(header.Key, header.Value);

            return response;
        }
    }

    public class OkWithResourceLinks<T> : OkWithHeadersContentResult<T> where T : class, IEnumerable<IIdentity> {
        public OkWithResourceLinks(T content, IContentNegotiator contentNegotiator, HttpRequestMessage request, IEnumerable<MediaTypeFormatter> formatters) : base(content, contentNegotiator, request, formatters) { }

        public OkWithResourceLinks(T content, ApiController controller, bool hasMore) : base(content, controller) {
            if (content == null)
                return;

            string firstId = content.Any() ? content.First().Id : String.Empty;
            string lastId = content.Any() ? content.Last().Id : String.Empty;

            bool includePrevious = true;
            bool includeNext = hasMore;
            bool hadBefore = false;
            bool hadAfter = false;

            var previousParameters = Request.RequestUri.ParseQueryString();
            if (previousParameters["before"] != null)
                hadBefore = true;
            previousParameters.Remove("before");
            if (previousParameters["after"] != null)
                hadAfter = true;
            previousParameters.Remove("after");
            var nextParameters = new NameValueCollection(previousParameters);
            
            previousParameters.Add("before", firstId);
            nextParameters.Add("after", lastId);

            if (hadBefore && !content.Any()) {
                // are we currently before the first page?
                includePrevious = false;
                includeNext = true;
                nextParameters.Remove("after");
            } else if (!hadBefore && !hadAfter) {
                // are we at the first page?
                includePrevious = false;
            }

            string baseUrl = Request.RequestUri.ToString().Replace(Request.RequestUri.Query, "");

            string previousLink = String.Format("<{0}?{1}>; rel=\"previous\"", baseUrl, previousParameters.ToQueryString());
            string nextLink = String.Format("<{0}?{1}>; rel=\"next\"", baseUrl, nextParameters.ToQueryString());
            var links = new List<string>();
            if (includePrevious)
                links.Add(previousLink);
            if (includeNext)
                links.Add(nextLink);

            if (links.Count == 0)
                return;

            Headers = new Dictionary<string, IEnumerable<string>> {
                { "Link", links.ToArray() }
            };
        }
    }
}
