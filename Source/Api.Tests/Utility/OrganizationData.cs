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
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using MongoDB.Bson;

namespace Exceptionless.Tests.Utility {
    internal static class OrganizationData {
        public static IEnumerable<Organization> GenerateOrganizations(int count = 10, bool generateId = false, string id = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateOrganization(generateId, id);
        }

        public static List<Organization> GenerateSampleOrganizations() {
            return new List<Organization> {
                GenerateSampleOrganization(),
                GenerateOrganization(id: TestConstants.OrganizationId2, inviteEmail: TestConstants.InvitedOrganizationUserEmail),
                GenerateOrganization(id: TestConstants.OrganizationId3, inviteEmail: TestConstants.InvitedOrganizationUserEmail),
                GenerateOrganization(id: TestConstants.OrganizationId4, inviteEmail: TestConstants.InvitedOrganizationUserEmail),
                GenerateOrganization(id: TestConstants.SuspendedOrganizationId, inviteEmail: TestConstants.InvitedOrganizationUserEmail, isSuspended: true),
            };
        }

        public static Organization GenerateSampleOrganization() {
            return GenerateOrganization(id: TestConstants.OrganizationId, name: "Acme", inviteEmail: TestConstants.InvitedOrganizationUserEmail);
        }

        public static Organization GenerateOrganization(bool generateId = false, string name = null, string id = null, string inviteEmail = null, bool isSuspended = false) {
            var organization = new Organization {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.OrganizationId : id,
                Name = name ?? String.Format("Organization{0}", id),
                IsSuspended = isSuspended,
                PlanId = BillingManager.UnlimitedPlan.Id
            };

            if (!String.IsNullOrEmpty(inviteEmail)) {
                organization.Invites.Add(new Invite {
                    EmailAddress = inviteEmail,
                    Token = Guid.NewGuid().ToString()
                });
            }

            return organization;
        }
    }
}