using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.ConnectionPool;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout
    {
        private IElasticsearchClient _client;

        public string ConnectionName { get; set; }
        public string Host { get; set; }

        [DefaultValue(9200)]
        public int Port { get; set; }

        public Layout Index { get; set; }

        [RequiredParameter]
        public Layout DocumentType { get; set; }

        [ArrayParameter(typeof(ElasticSearchField), "field")]
        public IList<ElasticSearchField> Fields { get; private set; }

        public ElasticSearchTarget()
        {
            Port = 9200;
            Host = "localhost";
            DocumentType = "logevent";
            Index = "logstash-${date:format=yyyy.MM.dd}";
            Fields = new List<ElasticSearchField>();
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            ConnectionStringSettings connectionStringSettings = null;
            if (!String.IsNullOrEmpty(ConnectionName))
                connectionStringSettings = ConfigurationManager.ConnectionStrings[ConnectionName];

            var node = connectionStringSettings != null ? new Uri(connectionStringSettings.ConnectionString) : new Uri(string.Format("http://{0}:{1}", Host, Port));
            var connectionPool = new SniffingConnectionPool(new[] { node });
            var config = new ConnectionConfiguration(connectionPool);
            _client = new ElasticsearchClient(config);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            Write(new []{logEvent});
        }

        protected override void Write(AsyncLogEventInfo[] logEvents)
        {
            SendBatch(logEvents);
        }

        private void SendBatch(IEnumerable<AsyncLogEventInfo> events)
        {
            var logEvents = events.Select(e => e.LogEvent);
            var payload = new List<object>();

            foreach (var logEvent in logEvents)
            {
                var document = new Dictionary<string, object>();
                document.Add("@timestamp", logEvent.TimeStamp);
                document.Add("level", logEvent.Level.Name);
                if (logEvent.Exception != null)
                    document.Add("exception", logEvent.Exception.ToString());
                document.Add("message", Layout.Render(logEvent));
                foreach (var field in Fields) {
                    var renderedField = field.Layout.Render(logEvent);
                    if (!String.IsNullOrWhiteSpace(renderedField))
                        document[field.Name] = renderedField;
                }

                payload.Add(new { index = new { _index = Index.Render(logEvent).ToLowerInvariant(), _type = DocumentType.Render(logEvent) } });
                payload.Add(document);
            }

            try {
                var result = _client.Bulk(payload);
                if (!result.Success)
                    InternalLogger.Error("Failed to send log messages to ElasticSearch: status={0} message=\"{1}\"", result.HttpStatusCode, result.OriginalException.Message);
            } catch (Exception ex) {
                InternalLogger.Error("Error while sending log messages to ElasticSearch: message=\"{0}\"", ex.Message);
            }
        }
    }
}
