using System;
using System.Collections.Generic;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

namespace Exceptionless.NLog {
    [Target("Exceptionless")]
    public class ExceptionlessTarget : TargetWithLayout {
        private ExceptionlessClient _client;

        public string ApiKey { get; set; }
        public string ServerUrl { get; set; }

        [ArrayParameter(typeof(ExceptionlessField), "field")]
        public IList<ExceptionlessField> Fields { get; private set; }

        public ExceptionlessTarget() {
            Fields = new List<ExceptionlessField>();
        }

        protected override void InitializeTarget() {
            base.InitializeTarget();

            if (!String.IsNullOrEmpty(ApiKey) || !String.IsNullOrEmpty(ServerUrl))
                _client = new ExceptionlessClient(config => {
                    config.ApiKey = ApiKey;
                    config.ServerUrl = ServerUrl;
                });
            else
                _client = ExceptionlessClient.Default;
        }

        protected override void Write(AsyncLogEventInfo info) {
            var builder = _client.CreateFromLogEvent(info.LogEvent);
            foreach (var field in Fields) {
                var renderedField = field.Layout.Render(info.LogEvent);
                if (!String.IsNullOrWhiteSpace(renderedField))
                    builder.AddObject(renderedField, field.Name);
            }

            builder.Submit();
        }
    }
}
