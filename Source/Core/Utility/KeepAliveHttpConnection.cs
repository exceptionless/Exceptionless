using System;
using System.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.Connection.Configuration;

namespace Exceptionless.Core.Utility {
    public class KeepAliveHttpConnection : HttpConnection {
        public KeepAliveHttpConnection(IConnectionConfigurationValues settings) : base(settings) { }
        
        protected override HttpWebRequest CreateHttpWebRequest(Uri uri, string method, byte[] data, IRequestConfiguration requestSpecificConfig) {
            var request = base.CreateHttpWebRequest(uri, method, data, requestSpecificConfig);
            request.ServicePoint.SetTcpKeepAlive(true, 30 * 1000, 2000);
            return request;
        }
    }
}