using System;

namespace Exceptionless.Core.Models {
    public class SummaryData {
        public SummaryData(string id, string templateKey, object data) {
            Id = id;
            TemplateKey = templateKey;
            Data = data;
        }

        public string Id { get; private set; }
        public string TemplateKey { get; private set; }
        public object Data { get; private set; }
    }
}