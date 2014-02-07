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
using System.Security.Principal;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;
using IIdentity = System.Security.Principal.IIdentity;

namespace Exceptionless.Core.Authorization {
    public class ExceptionlessPrincipal : IPrincipal {
        private readonly HashSet<string> _roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ExceptionlessPrincipal(Project project) {
            Project = project;
            Identity = new GenericIdentity(project.Id, "ApiKey");
            _roles.Add(AuthorizationRoles.Client);
        }

        public ExceptionlessPrincipal(User user) {
            UserEntity = user;
            Identity = new GenericIdentity(user.EmailAddress, "User");
            _roles.Add(AuthorizationRoles.User);
            if (user.Roles != null)
                _roles.AddRange(user.Roles);
        }

        public User UserEntity { get; private set; }

        // Only populated when you are logged in from the client.
        public Project Project { get; private set; }

        public bool IsInRole(string role) {
            return _roles.Contains(role);
        }

        public bool IsInOrganization(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            if (Project != null)
                return String.Equals(Project.OrganizationId, organizationId);

            if (UserEntity != null)
                return UserEntity.OrganizationIds.Contains(organizationId);

            return false;
        }

        public bool CanAccessOrganization(string organizationId) {
            return IsInRole(AuthorizationRoles.GlobalAdmin) || IsInOrganization(organizationId);
        }

        public IEnumerable<string> GetAssociatedOrganizationIds() {
            var items = new List<string>();

            if (UserEntity != null)
                items.AddRange(UserEntity.OrganizationIds);
            else if (Project != null)
                items.Add(Project.OrganizationId);

            return items;
        }

        public IIdentity Identity { get; private set; }
    }
}