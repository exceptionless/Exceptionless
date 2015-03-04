using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;

namespace NLog.Fluent {
    public static class LogBuilderExtensions {
        public static LogBuilder Critical(this LogBuilder builder, bool isCritical = true) {
            return isCritical ? builder.Tag(Event.KnownTags.Critical) : builder;
        }

        public static LogBuilder Tag(this LogBuilder builder, string tag) {
            return builder.Tag(new[] { tag });
        }

        public static LogBuilder Tag(this LogBuilder builder, IEnumerable<string> tags) {
            var tagList = new List<string>();
            if (builder.LogEventInfo.Properties.ContainsKey("tags") && builder.LogEventInfo.Properties["tags"] is List<string>)
                tagList = builder.LogEventInfo.Properties["tags"] as List<string>;

            foreach (string tag in tags) {
                if (!tagList.Any(s => s.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                    tagList.Add(tag);
            }

            return builder.Property("tags", tagList);
        }

        public static LogBuilder Project(this LogBuilder builder, string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return builder;

            return builder.Property("project", projectId);
        }

        public static LogBuilder Organization(this LogBuilder builder, string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return builder;

            return builder.Property("organization", organizationId);
        }
    }
}