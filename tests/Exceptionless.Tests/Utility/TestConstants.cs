using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;

namespace Exceptionless.Tests.Utility {
    public static class TestConstants {
        public const string ProjectId = SampleDataService.TEST_PROJECT_ID;
        public const string ProjectIdWithNoRoles = "1ecd0826e447ad1e78877a66";
        public const string SuspendedProjectId = "1ecd0826e446dd1e78877ab3";
        public const string InvalidProjectId = "0ecd0826e447ad1e78877ab0";

        public const string OrganizationId = SampleDataService.TEST_ORG_ID;
        public const string OrganizationId2 = "1ecd0826e447ad1e78877666";
        public const string OrganizationId3 = "1ecd0826e447ad1e78877777";
        public const string OrganizationId4 = "1ecd0826e447ad1e78877888";
        public const string SuspendedOrganizationId = "1ecd0826e447ad1e78877999";
        public const string InvalidOrganizationId = "0ecd0446e447ad1e78877ab0";
        public const string InvitedOrganizationUserEmail = "invited@exceptionless.io";
        public const string InvitedOrganizationUserEmail2 = "invited2@exceptionless.io";
        public const string InvalidInvitedOrganizationUserEmail = "invalid-invite@exceptionless.io";

        public const string UserId = "1ecd0826e447ad1e78822555";
        public const string UserId2 = "1ecd0826e447ad1e78822666";
        public const string UserEmail = SampleDataService.TEST_USER_EMAIL;
        public const string UserEmail2 = "user2@exceptionless.io";
        public const string UserPassword = SampleDataService.TEST_USER_PASSWORD;
        public static readonly string UserPasswordHash = UserPassword.ToSHA256();
        public const string UserIdWithNoRoles = "1ecd0826e447ad1e78822556";
        public const string UserEmailWithNoRoles = "user.noroles@exceptionless.io";
        public const string InvalidUserId = "0ec44826e447ad1e78444ab0";
        public const string InvalidUserEmail = "invalid@exceptionless.io";

        public const string EventId = "22cd0826e447a44e78877a22";

        public const string StackId = "1ecd0826e447a44e78877ab1";
        public const string StackId2 = "2ecd0826e447a44e78877ab2";
        public const string InvalidStackId = "0ecd0826e447ad1e78877ab0";
        public const string TokenId = "88cd0826e447a44e78877ab1";

        public const string ApiKey = SampleDataService.TEST_API_KEY;
        public const string UserApiKey = SampleDataService.TEST_USER_API_KEY;
        public const string SuspendedApiKey = "5ccd0826e447ad1e78877ab4";
        public const string InvalidApiKey = "1dddddd6e447ad1e78877ab1";

        public static readonly List<string> ExceptionTypes = new List<string> {
            "System.NullReferenceException",
            "System.ApplicationException",
            "System.AggregateException",
            "System.Exception",
            "System.ArgumentException",
            "System.InvalidArgumentException",
            "System.InvalidOperationException"
        };

        public static readonly List<string> EventTags = new List<string> {
            "Tag1",
            "Tag2",
            "Tag3",
            "Tag4",
            "Tag5"
        };

        public static readonly List<string> ProjectIds = new List<string> {
            ProjectId,
            InvalidProjectId,
            ProjectIdWithNoRoles
        };

        public static readonly List<string> Namespaces = new List<string> {
            "System",
            "System.IO",
            "CodeSmith",
            "CodeSmith.Generator",
            "SomeOther.Blah"
        };

        public static readonly List<string> TypeNames = new List<string> {
            "DateTime",
            "SomeType",
            "ProjectGenerator"
        };

        public static readonly List<string> MethodNames = new List<string> {
            "SomeMethod",
            "GenerateCode"
        };
    }
}