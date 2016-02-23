using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data {
    public class StackingInfo {
        public StackingInfo() {
            SignatureData = new Dictionary<string, string>();
        }

        public StackingInfo(string title) : this() {
            if (!String.IsNullOrWhiteSpace(title))
                Title = title.Trim();
        }

        public StackingInfo(string title, IDictionary<string, string> signatureData) : this(title) {
            if (signatureData != null && signatureData.Count > 0)
                SignatureData.AddRange(signatureData);
        }

        public StackingInfo(IDictionary<string, string> signatureData) : this(null, signatureData) { }

        /// <summary>
        /// Stack Title (defaults to the event message)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Key value pair that determines how the event is stacked.
        /// </summary>
        public IDictionary<string, string> SignatureData { get; set; }
    }
}