using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions {
    public static class RequestInfoExtensions {
        public static RequestInfo ApplyDataExclusions(this RequestInfo request, IList<string> exclusions, int maxLength = 1000) {
            if (request == null)
                return null;

            request.Cookies = ApplyExclusions(request.Cookies, exclusions, maxLength);
            request.QueryString = ApplyExclusions(request.QueryString, exclusions, maxLength);
            request.PostData = ApplyPostDataExclusions(request.PostData, exclusions, maxLength);

            return request;
        }

        private static object ApplyPostDataExclusions(object data, IEnumerable<string> exclusions, int maxLength) {
            if (data == null)
                return null;

            var dictionary = data as Dictionary<string, string>;
            if (dictionary == null && data is string) {
                try {
                    dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>((string)data);
                } catch (Exception) {}
            }

            return dictionary != null ? ApplyExclusions(dictionary, exclusions, maxLength) : data;
        }

        private static Dictionary<string, string> ApplyExclusions(Dictionary<string, string> dictionary, IEnumerable<string> exclusions, int maxLength) {
            if (dictionary == null || dictionary.Count == 0)
                return dictionary;

            foreach (var key in dictionary.Keys.Where(k => String.IsNullOrEmpty(k) || CodeSmith.Core.Extensions.StringExtensions.AnyWildcardMatches(k, exclusions, true)).ToList())
                dictionary.Remove(key);

            foreach (var key in dictionary.Where(kvp => kvp.Value != null && kvp.Value.Length > maxLength).Select(kvp => kvp.Key).ToList())
                dictionary[key] = String.Format("Value is too large to be included.");

            return dictionary;
        }
    }
}