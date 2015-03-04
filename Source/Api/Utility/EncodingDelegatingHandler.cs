using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Api.Utility {
    public class EncodingDelegatingHandler : DelegatingHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Method != HttpMethod.Get && request.Content != null
                && !(request.RequestUri.AbsolutePath.EndsWith("/events") && request.Method == HttpMethod.Post)
                && !(request.RequestUri.AbsolutePath.EndsWith("/error") && request.Method == HttpMethod.Post)
                && request.Content.Headers.ContentEncoding.Any()) {
                string encodingType = request.Content.Headers.ContentEncoding.First().ToLowerInvariant();
                if (encodingType == "gzip" || encodingType == "deflate")
                    request.Content = new CompressedContent(request.Content, encodingType);
            }

            return base.SendAsync(request, cancellationToken).ContinueWith(responseToCompleteTask => {
                HttpResponseMessage response = responseToCompleteTask.Result;

                if (response.RequestMessage != null
                    && response.RequestMessage.Headers != null
                    && response.RequestMessage.Headers.AcceptEncoding != null
                    && response.RequestMessage.Headers.AcceptEncoding.Count > 0) {
                    string encodingType = response.RequestMessage.Headers.AcceptEncoding.First().Value;

                    if (response.Content != null)
                        response.Content = new CompressedContent(response.Content, encodingType);
                }

                return response;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    public class CompressedContent : HttpContent {
        private readonly HttpContent _originalContent;
        private readonly string _encodingType;

        public CompressedContent(HttpContent content, string encodingType) {
            if (content == null)
                throw new ArgumentNullException("content");

            if (encodingType == null)
                throw new ArgumentNullException("encodingType");

            _originalContent = content;
            _encodingType = encodingType.ToLowerInvariant();

            if (_encodingType != "gzip" && _encodingType != "deflate")
                throw new InvalidOperationException(String.Format("Encoding '{0}' is not supported. Only supports gzip or deflate encoding.", _encodingType));

            // copy the headers from the original content
            foreach (KeyValuePair<string, IEnumerable<string>> header in _originalContent.Headers)
                Headers.TryAddWithoutValidation(header.Key, header.Value);

            Headers.ContentEncoding.Clear();
            Headers.ContentEncoding.Add(encodingType);
        }

        protected override bool TryComputeLength(out long length) {
            length = 0;

            return false;
        }

        private async Task<Stream> CreateDeflateStream() {
            var stream = await _originalContent.ReadAsStreamAsync();

            if (_encodingType == "gzip")
                return new GZipStream(stream, CompressionMode.Decompress);

            if (_encodingType == "deflate")
                return new DeflateStream(stream, CompressionMode.Decompress);

            throw new NotSupportedException("Compression type not supported or stream isn't compressed");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) {
            Stream compressedStream = null;
            
            switch (_encodingType) {
                case "gzip":
                    compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
                    break;
                case "deflate":
                    compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
                    break;
            }

            return _originalContent.CopyToAsync(compressedStream).ContinueWith(tsk => {
                if (compressedStream != null)
                    compressedStream.Dispose();
            });
        }

        protected override Task<Stream> CreateContentReadStreamAsync() {
            return CreateDeflateStream();
        }

        protected override void Dispose(bool disposing) {
            _originalContent.Dispose();
        }
    }
}