using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    public class User : IIdentity, IHaveDates {
        public User() {
            IsActive = true;
            OAuthAccounts = new Collection<OAuthAccount>();
            Roles = new Collection<string>();
            OrganizationIds = new Collection<string>();
            EmailNotificationsEnabled = true;
        }

        /// <summary>
        /// Unique id that identifies an user.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The organizations that the user has access to.
        /// </summary>
        public ICollection<string> OrganizationIds { get; set; }

        public string Password { get; set; }
        public string Salt { get; set; }
        public string PasswordResetToken { get; set; }
        public DateTime PasswordResetTokenExpiration { get; set; }
        public ICollection<OAuthAccount> OAuthAccounts { get; set; }

        /// <summary>
        /// Gets or sets the users Full Name.
        /// </summary>
        public string FullName { get; set; }

        public string EmailAddress { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
        public bool IsEmailAddressVerified { get; set; }
        public string VerifyEmailAddressToken { get; set; }
        public DateTime VerifyEmailAddressTokenExpiration { get; set; }

        /// <summary>
        /// Gets or sets the users active state.
        /// </summary>
        public bool IsActive { get; set; }

        public ICollection<string> Roles { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
