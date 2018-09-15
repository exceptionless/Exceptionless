using System;
using System.Collections.Generic;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class FolderConnectionString : DefaultConnectionString {
        public const string ProviderName = "folder";

        public FolderConnectionString(string connectionString, IDictionary<string, string> settings) : base(connectionString) {
            if (!settings.TryGetValue("path", out string path) || !String.IsNullOrEmpty(path))
                path = "|DataDirectory|\\storage";

            StorageFolder = PathHelper.ExpandPath(path);
        }

        public string StorageFolder { get; }
    }
}
