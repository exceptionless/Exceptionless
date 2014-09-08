#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using CodeSmith.Core.Extensions;

namespace Exceptionless.Tests.Utility {
    public static class TestConstants {
        public const string ProjectId = "1ecd0826e447ad1e78877ab2";
        public const string ProjectIdWithNoRoles = "1ecd0826e447ad1e78877a66";
        public const string SuspendedProjectId = "1ecd0826e446dd1e78877ab3";
        public const string InvalidProjectId = "0ecd0826e447ad1e78877ab0";

        public const string OrganizationId = "1ecd0826e447ad1e78877555";
        public const string OrganizationId2 = "1ecd0826e447ad1e78877666";
        public const string OrganizationId3 = "1ecd0826e447ad1e78877777";
        public const string OrganizationId4 = "1ecd0826e447ad1e78877888";
        public const string SuspendedOrganizationId = "1ecd0826e447ad1e78877999";
        public const string InvalidOrganizationId = "0ecd0446e447ad1e78877ab0";
        public const string InvitedOrganizationUserEmail = "invited@exceptionless.com";
        public const string InvitedOrganizationUserEmail2 = "invited2@exceptionless.com";
        public const string InvalidInvitedOrganizationUserEmail = "invalid-invite@exceptionless.com";

        public const string UserId = "1ecd0826e447ad1e78822555";
        public const string UserId2 = "1ecd0826e447ad1e78822666";
        public const string UserEmail = "user1@exceptionless.com";
        public const string UserEmail2 = "user2@exceptionless.com";
        public const string UserPassword = "2B5A3E6DFD3440CDA57E598F8B5D73B4";
        public static readonly string UserPasswordHash = UserPassword.ToSHA256();
        public const string UserIdWithNoRoles = "1ecd0826e447ad1e78822556";
        public const string UserEmailWithNoRoles = "user.noroles@exceptionless.com";
        public const string InvalidUserId = "0ec44826e447ad1e78444ab0";
        public const string InvalidUserEmail = "invalid@exceptionless.com";

        public const string StackId = "1ecd0826e447a44e78877ab1";
        public const string StackId2 = "2ecd0826e447a44e78877ab2";
        public const string InvalidStackId = "0ecd0826e447ad1e78877ab0";

        public const string ApiKey = "e3d51ea621464280bbcb79c11fd6483e";
        public const string ApiKey2 = "2ccd0826e447ad1e78877ab2";
        public const string ApiKey3 = "3ccd0826e447ad1e78877ab3";
        public const string ApiKey4 = "4ccd0826e447ad1e78877ab4";
        public const string SuspendedApiKey = "5ccd0826e447ad1e78877ab4";
        public const string InvalidApiKey = "1dddddd6e447ad1e78877ab1";

        public static readonly List<string> ExceptionTypes = new List<string> {
            "System.NullReferenceException",
            "System.ApplicationException",
            "System.AggregateException",
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