using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Api.Utility.Results {
    public class OkWithHeadersContentResult<T> : ObjectWithHeadersResult {
        public OkWithHeadersContentResult(T content, IHeaderDictionary headers = null) : base(content, headers) {
            StatusCode = StatusCodes.Status200OK;
        }
    }

    public class OkWithResourceLinks<TEntity> : OkWithHeadersContentResult<IEnumerable<TEntity>> where TEntity : class {
        //public OkWithResourceLinks(IEnumerable<TEntity> content, IHeaderDictionary headers = null) : base(content, headers) { }

        public OkWithResourceLinks(IEnumerable<TEntity> content, bool hasMore, int? page = null, Func<TEntity, string> pagePropertyAccessor = null, IHeaderDictionary headers = null, bool isDescending = false) 
            : this(content, hasMore, page, null, pagePropertyAccessor, headers, isDescending) {}

        public OkWithResourceLinks(IEnumerable<TEntity> content, bool hasMore, int? page = null, long? total = null, Func<TEntity, string> pagePropertyAccessor = null, IHeaderDictionary headers = null, bool isDescending = false) : base(content, headers) {
            Content = content;
            HasMore = hasMore;
            IsDescending = isDescending;
            Page = page;
            Total = total;
            PagePropertyAccessor = pagePropertyAccessor;
        }

        public IEnumerable<TEntity> Content { get; }
        public bool HasMore { get; }
        public bool IsDescending { get; }
        public int? Page { get; }
        public long? Total { get; }
        public Func<TEntity, string> PagePropertyAccessor { get; }

        public override void OnFormatting(ActionContext context) {
            if (Content != null) {
                List<string> links;
                if (Page.HasValue)
                    links = GetPagedLinks(new Uri(context.HttpContext.Request.GetDisplayUrl()), Page.Value, HasMore);
                else
                    links = GetBeforeAndAfterLinks(new Uri(context.HttpContext.Request.GetDisplayUrl()), Content, IsDescending, HasMore, PagePropertyAccessor);

                if (links.Count > 0)
                    Headers.Add("Link", links.ToArray());

                if (Total.HasValue)
                    Headers.Add("X-Result-Count", Total.ToString());
            }

            base.OnFormatting(context);
        }

        public static List<string> GetPagedLinks(Uri url, int page, bool hasMore) {
            bool includePrevious = page > 1;
            bool includeNext = hasMore;

            var previousParameters = HttpUtility.ParseQueryString(url.Query);
            previousParameters["page"] = (page - 1).ToString();
            var nextParameters = new NameValueCollection(previousParameters) {
                ["page"] = (page + 1).ToString()
            };

            string baseUrl = url.GetBaseUrl();

            string previousLink = $"<{baseUrl}?{previousParameters.ToQueryString()}>; rel=\"previous\"";
            string nextLink = $"<{baseUrl}?{nextParameters.ToQueryString()}>; rel=\"next\"";

            var links = new List<string>();
            if (includePrevious)
                links.Add(previousLink);
            if (includeNext)
                links.Add(nextLink);

            return links;
        }

        public static List<string> GetBeforeAndAfterLinks(Uri url, IEnumerable<TEntity> content, bool isDescending, bool hasMore, Func<TEntity, string> pagePropertyAccessor) {
            var contentList = content.ToList();
            if (pagePropertyAccessor == null && typeof(IIdentity).IsAssignableFrom(typeof(TEntity)))
                pagePropertyAccessor = e => ((IIdentity)e).Id;

            if (pagePropertyAccessor == null)
                return new List<string>();

            string firstId = contentList.Any() ? pagePropertyAccessor(!isDescending ? contentList.First() : contentList.Last()) : String.Empty;
            string lastId = contentList.Any() ? pagePropertyAccessor(!isDescending ? contentList.Last() : contentList.First()) : String.Empty;

            bool hasBefore = false;
            bool hasAfter = false;

            var previousParameters = HttpUtility.ParseQueryString(url.Query);
            if (previousParameters["before"] != null)
                hasBefore = true;
            previousParameters.Remove("before");
            if (previousParameters["after"] != null)
                hasAfter = true;
            previousParameters.Remove("after");
            var nextParameters = new NameValueCollection(previousParameters);

            previousParameters.Add("before", firstId);
            nextParameters.Add("after", lastId);

            bool includePrevious = hasBefore ? hasMore : true;
            bool includeNext = !hasBefore ? hasMore : true;
            if (hasBefore && !contentList.Any()) {
                // are we currently before the first page?
                includePrevious = false;
                includeNext = true;
                nextParameters.Remove("after");
            } else if (!hasBefore && !hasAfter) {
                // are we at the first page?
                includePrevious = false;
            }

            string baseUrl = url.GetBaseUrl();

            string previousLink = $"<{baseUrl}?{previousParameters.ToQueryString()}>; rel=\"previous\"";
            string nextLink = $"<{baseUrl}?{nextParameters.ToQueryString()}>; rel=\"next\"";

            var links = new List<string>();
            if (includePrevious)
                links.Add(previousLink);
            if (includeNext)
                links.Add(nextLink);

            return links;
        }
    }
}
