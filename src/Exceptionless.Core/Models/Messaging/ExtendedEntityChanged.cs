using System;
using System.Diagnostics;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Messaging.Models {
    [DebuggerDisplay("{Type} {ChangeType}: Id={Id}, OrganizationId={OrganizationId}, ProjectId={ProjectId}, StackId={StackId}")]
    public class ExtendedEntityChanged : EntityChanged {
        private ExtendedEntityChanged() { } // Ensure create is used.

        public string OrganizationId { get; private set; }
        public string ProjectId { get; private set; }
        public string StackId { get; private set; }

        public static ExtendedEntityChanged Create(EntityChanged entityChanged, bool removeWhenSettingProperties = true) {
            var model = new ExtendedEntityChanged {
                Id = entityChanged.Id,
                Type = entityChanged.Type,
                ChangeType = entityChanged.ChangeType,
                Data = entityChanged.Data
            };

            if (model.Data.TryGetValue(KnownKeys.OrganizationId, out var organizationId)) {
                model.OrganizationId = organizationId.ToString();
                if (removeWhenSettingProperties)
                    model.Data.Remove(KnownKeys.OrganizationId);
            }

            if (model.Data.TryGetValue(KnownKeys.ProjectId, out var projectId)) {
                model.ProjectId = projectId.ToString();
                if (removeWhenSettingProperties)
                    model.Data.Remove(KnownKeys.ProjectId);
            }

            if (model.Data.TryGetValue(KnownKeys.StackId, out var stackId)) {
                model.StackId = stackId.ToString();
                if (removeWhenSettingProperties)
                    model.Data.Remove(KnownKeys.StackId);
            }

            return model;
        }

        public class KnownKeys {
            public const string OrganizationId = nameof(OrganizationId);
            public const string ProjectId = nameof(ProjectId);
            public const string StackId = nameof(StackId);
            public const string UserId = nameof(UserId);
            public const string IsAuthenticationToken = nameof(IsAuthenticationToken);
        }
    }
}