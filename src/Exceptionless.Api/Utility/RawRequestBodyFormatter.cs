using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Api.Utility {
    public class RawRequestBodyFormatter : InputFormatter {
        public RawRequestBodyFormatter() {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/plain"));
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
        }

        public override bool CanRead(InputFormatterContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            MediaTypeHeaderValue.TryParse(context.HttpContext.Request.ContentType, out var contentTypeHeader);
            var contentType = contentTypeHeader?.MediaType.ToString();
            if (String.IsNullOrEmpty(contentType) || contentType == "text/plain" || contentType == "application/octet-stream")
                return true;

            return false;
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context) {
            var request = context.HttpContext.Request;

            MediaTypeHeaderValue.TryParse(request.ContentType, out var contentTypeHeader);
            string contentType = contentTypeHeader?.MediaType.ToString();

            if (String.IsNullOrEmpty(contentType) || contentType == "text/plain") {
                using (var reader = new StreamReader(request.Body)) {
                    var content = await reader.ReadToEndAsync();
                    return await InputFormatterResult.SuccessAsync(content);
                }
            }
            if (contentType == "application/octet-stream") {
                using (var ms = new MemoryStream(2048)) {
                    await request.Body.CopyToAsync(ms);
                    var content = ms.ToArray();
                    return await InputFormatterResult.SuccessAsync(content);
                }
            }

            return await InputFormatterResult.FailureAsync();
        }
    }
}