﻿using System;
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
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Method != HttpMethod.Get && request.Content != null
                && !(request.RequestUri.AbsolutePath.EndsWith("/events") && request.Method == HttpMethod.Post)
                && !(request.RequestUri.AbsolutePath.EndsWith("/error") && request.Method == HttpMethod.Post)
                && request.Content.Headers.ContentEncoding.Any()) {
                string encodingType = request.Content.Headers.ContentEncoding.First().ToLowerInvariant();
                if (encodingType == "gzip" || encodingType == "deflate")
                    request.Content = new CompressedContent(request.Content, encodingType);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Content != null) {
                string encodingType = response.RequestMessage?.Headers?.AcceptEncoding?.FirstOrDefault()?.Value;
                if (encodingType == "gzip" || encodingType == "deflate")
                    response.Content = new CompressedContent(response.Content, encodingType);
            }

            return response;
        }
    }

    public class CompressedContent : HttpContent {
        private readonly HttpContent _originalContent;
        private readonly string _encodingType;

        public CompressedContent(HttpContent content, string encodingType) {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (encodingType == null)
                throw new ArgumentNullException(nameof(encodingType));

            _originalContent = content;
            _encodingType = encodingType.ToLowerInvariant();

            if (_encodingType != "gzip" && _encodingType != "deflate")
                throw new InvalidOperationException($"Encoding '{_encodingType}' is not supported. Only supports gzip or deflate encoding.");

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

        private async Task<Stream> CreateDeflateStreamAsync() {
            var stream = await _originalContent.ReadAsStreamAsync();

            if (_encodingType == "gzip")
                return new GZipStream(stream, CompressionMode.Decompress);

            if (_encodingType == "deflate")
                return new DeflateStream(stream, CompressionMode.Decompress);

            throw new NotSupportedException("Compression type not supported or stream isn't compressed");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context) {
            Stream compressedStream = null;
            
            switch (_encodingType) {
                case "gzip":
                    compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
                    break;
                case "deflate":
                    compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
                    break;
            }

            if (compressedStream != null) {
                await _originalContent.CopyToAsync(compressedStream);
                compressedStream.Dispose();
            }
        }

        protected override Task<Stream> CreateContentReadStreamAsync() {
            return CreateDeflateStreamAsync();
        }

        protected override void Dispose(bool disposing) {
            _originalContent.Dispose();
        }
    }
}