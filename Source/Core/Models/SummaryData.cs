using System;

namespace Exceptionless.Core.Models {
    public class SummaryData {
        public SummaryData(string templateKey, object data = null) {
            TemplateKey = templateKey;
            Data = data;
        }

        public string TemplateKey { get; private set; }
        public object Data { get; private set; }
    }
}