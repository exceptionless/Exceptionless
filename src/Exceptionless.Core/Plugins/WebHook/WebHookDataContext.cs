using System;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.WebHook {
    public class WebHookDataContext : ExtensibleObject {
        public WebHookDataContext(string version, PersistentEvent ev, Organization organization = null, Project project = null, Stack stack = null, bool isNew = false, bool isRegression = false) {
            Version = version ?? throw new ArgumentException("Version cannot be null.", nameof(version));
            Organization = organization;
            Project = project;
            Stack = stack;
            Event = ev ?? throw new ArgumentException("Event cannot be null.", nameof(ev));
            IsNew = isNew;
            IsRegression = isRegression;
        }

        public WebHookDataContext(string version, Stack stack, Organization organization = null, Project project = null, bool isNew = false, bool isRegression = false) {
            Version = version ?? throw new ArgumentException("Version cannot be null.", nameof(version));
            Organization = organization;
            Project = project;
            Stack = stack ?? throw new ArgumentException("Stack cannot be null.", nameof(stack));
            IsNew = isNew;
            IsRegression = isRegression;
        }

        public PersistentEvent Event { get; set; }
        public Stack Stack { get; set; }
        public Organization Organization { get; set; }
        public Project Project { get; set; }

        public string Version { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
    }
}