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
using System.Linq;
using System.Web;
using CodeSmith.Core.Helpers;
using DotNetOpenAuth.AspNet;
using Exceptionless.Models;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace Exceptionless.Membership {
    public class MembershipProvider : IMembershipProvider, IOpenAuthDataProvider {
        private readonly IApplicationEnvironment _applicationEnvironment = new AspnetEnvironment();
        private readonly MongoCollection<User> _userCollection;
        private readonly IMembershipSecurity _encoder = new DefaultMembershipSecurity();

        public MembershipProvider(MongoCollection<User> userCollection) {
            _userCollection = userCollection;
        }

        public bool Login(string emailAddress, string password, bool remember = false) {
            User user = GetUserByEmailAddress(emailAddress);
            if (user == null || !user.IsActive)
                return false;

            if (String.IsNullOrEmpty(user.Salt))
                throw new InvalidOperationException("This account was created via an OAuth provider. Please login with with your Microsoft, Google or Facebook account.");

            string encodedPassword = _encoder.GetSaltedHash(password, user.Salt);
            bool passed = encodedPassword.Equals(user.Password);
            if (passed) {
                _applicationEnvironment.IssueAuthTicket(emailAddress, remember);
                return true;
            }
            return false;
        }

        public void Logout() {
            _applicationEnvironment.RevokeAuthTicket();
        }

        public void CreateAccount(User user) {
            User existingUser = GetUserByEmailAddress(user.EmailAddress);
            if (existingUser != null)
                throw new MembershipException(MembershipStatus.DuplicateUserName);

            user.Salt = user.Salt ?? _encoder.GenerateSalt();
            user.Password = _encoder.GetSaltedHash(user.Password, user.Salt);
            user.IsActive = true;

            Save(user);

            _applicationEnvironment.IssueAuthTicket(user.EmailAddress, false);
        }

        public void UpdateAccount(User user) {
            Save(user);
            _applicationEnvironment.IssueAuthTicket(user.EmailAddress, false);
        }

        public bool HasLocalAccount(string emailAddress) {
            User user = GetUserByEmailAddress(emailAddress);
            return user != null && !String.IsNullOrEmpty(user.Password);
        }

        public string GenerateVerifyEmailToken(string emailAddress, int tokenExpirationInMinutesFromNow = 1440) {
            User user = GetUserByEmailAddress(emailAddress);
            if (user == null)
                throw new MembershipException(MembershipStatus.InvalidUserName);

            user.VerifyEmailAddressToken = GenerateToken();
            user.VerifyEmailAddressTokenExpiration = DateTime.Now.AddMinutes(tokenExpirationInMinutesFromNow);
            Save(user);

            return user.VerifyEmailAddressToken;
        }

        public bool VerifyEmailAddress(string verifyEmailToken) {
            if (String.IsNullOrEmpty(verifyEmailToken))
                return false;

            User user = _userCollection.FindOne(Query<User>.EQ(u => u.VerifyEmailAddressToken, verifyEmailToken));
            if (user == null)
                return false;

            return user.VerifyEmailAddressTokenExpiration != DateTime.MinValue && user.VerifyEmailAddressTokenExpiration > DateTime.Now;
        }

        public bool ChangePassword(string emailAddress, string oldPassword, string newPassword) {
            User user = GetUserByEmailAddress(emailAddress);
            string encodedPassword = _encoder.GetSaltedHash(oldPassword, user.Salt);
            if (!encodedPassword.Equals(user.Password))
                return false;

            user.Password = _encoder.GetSaltedHash(newPassword, user.Salt);
            Save(user);
            return true;
        }

        public void SetLocalPassword(string emailAddress, string newPassword) {
            User user = GetUserByEmailAddress(emailAddress);
            if (!String.IsNullOrEmpty(user.Password))
                throw new MembershipException("SetLocalPassword can only be used on accounts that currently don't have a local password.");

            user.Salt = _encoder.GenerateSalt();
            user.Password = _encoder.GetSaltedHash(newPassword, user.Salt);
            Save(user);
        }

        public string GeneratePasswordResetToken(string emailAddress, int tokenExpirationInMinutesFromNow = 1440) {
            User user = GetUserByEmailAddress(emailAddress);
            if (user == null)
                throw new MembershipException(MembershipStatus.InvalidUserName);

            user.PasswordResetToken = GenerateToken();
            user.PasswordResetTokenExpiration = DateTime.Now.AddMinutes(tokenExpirationInMinutesFromNow);
            Save(user);

            return user.PasswordResetToken;
        }

        public bool CancelPasswordReset(string passwordResetToken) {
            User user = GetUserByPasswordResetToken(passwordResetToken);
            if (user == null)
                return false;

            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiration = DateTime.MinValue;
            Save(user);

            return true;
        }

        public bool ResetPassword(string passwordResetToken, string newPassword) {
            User user = GetUserByPasswordResetToken(passwordResetToken);
            if (user == null)
                return false;

            if (String.IsNullOrEmpty(user.Salt))
                user.Salt = _encoder.GenerateSalt();
            user.Password = _encoder.GetSaltedHash(newPassword, user.Salt);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiration = DateTime.MinValue;
            Save(user);

            return true;
        }

        public IEnumerable<OAuthAccount> GetOAuthAccountsFromEmailAddress(string emailAddress) {
            User user = GetUserByEmailAddress(emailAddress);
            if (user == null)
                return new OAuthAccount[0];

            return user.OAuthAccounts;
        }

        public bool DeleteOAuthAccount(string provider, string providerUserId) {
            User user = GetUserByOAuthProvider(provider, providerUserId);
            if (user == null)
                return false;

            // allow the account to be deleted only if there is a local password or there is more than one external login
            if (user.OAuthAccounts.Count > 1 || !String.IsNullOrEmpty(user.Password)) {
                OAuthAccount account = user.OAuthAccounts.Single(o => o.Provider == provider && o.ProviderUserId == providerUserId);
                user.OAuthAccounts.Remove(account);
                Save(user);
                return true;
            }

            return false;
        }

        public User CreateOAuthAccount(OAuthAccount account, User user) {
            var emailAddressesToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!String.IsNullOrWhiteSpace(user.EmailAddress))
                emailAddressesToCheck.Add(user.EmailAddress);

            var verifiedEmailAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!String.IsNullOrWhiteSpace(account.Username) && account.Username.Contains('@'))
                verifiedEmailAddresses.Add(account.Username);

            foreach (string key in new[] { "email", "preferred_email", "account_email", "personal_email" }) {
                if (account.ExtraData.ContainsKey(key) && !String.IsNullOrWhiteSpace(account.ExtraData[key]) && account.ExtraData[key].Contains('@'))
                    verifiedEmailAddresses.Add(account.ExtraData[key]);
            }

            if (verifiedEmailAddresses.Count > 0)
                emailAddressesToCheck.UnionWith(verifiedEmailAddresses);

            // Try to find existing user accounts and add this OAuth account to them.
            // TODO: Shouldn't we check to see if there are multiple existing users..
            User existingUser = GetUserByEmailAddress(user.EmailAddress);
            foreach (string email in emailAddressesToCheck) {
                User u = GetUserByEmailAddress(email);
                if (u != null) {
                    existingUser = u;

                    if (u.IsEmailAddressVerified)
                        verifiedEmailAddresses.Add(u.EmailAddress);
                }
            }

            if (existingUser != null)
                user = existingUser;

            User oauthUser = GetUserByOAuthProvider(account.Provider, account.ProviderUserId);
            if (user.OAuthAccounts == null)
                user.OAuthAccounts = new HashSet<OAuthAccount>();

            // The OAuth account was already associated to this user.
            if (oauthUser != null && oauthUser.Id == user.Id)
                return oauthUser;

            // Move OAuth account to new user.
            if (oauthUser != null) {
                OAuthAccount duplicateAccount = oauthUser.OAuthAccounts.Single(o => o.Provider == account.Provider && o.ProviderUserId == account.ProviderUserId);
                oauthUser.OAuthAccounts.Remove(duplicateAccount);
                Save(oauthUser);
            }

            user.OAuthAccounts.Add(account);
            user.IsActive = true;
            user.IsEmailAddressVerified = user.IsEmailAddressVerified || verifiedEmailAddresses.Contains(user.EmailAddress);

            // TODO: Should we be updating the users full name from the oauth extended data?

            Save(user);

            return user;
        }

        private static string GenerateToken() {
            return RandomHelper.GetPronouncableString(10);
        }

        public string GetUserNameFromOpenAuth(string provider, string providerUserId) {
            User user = GetUserByOAuthProvider(provider, providerUserId);
            return user != null ? user.EmailAddress : String.Empty;
        }

        public AuthenticationClientData GetOAuthClientData(string providerName) {
            if (!_authenticationClients.ContainsKey(providerName))
                return null;

            return _authenticationClients[providerName];
        }

        public ICollection<AuthenticationClientData> RegisteredClientData { get { return _authenticationClients.Values; } }

        public void RequestOAuthAuthentication(string provider, string returnUrl) {
            AuthenticationClientData client = _authenticationClients[provider];
            _applicationEnvironment.RequestAuthentication(client.AuthenticationClient, this, returnUrl);
        }

        public AuthenticationResult VerifyOAuthAuthentication(string returnUrl) {
            string providerName = _applicationEnvironment.GetOAuthPoviderName();
            if (String.IsNullOrEmpty(providerName))
                return AuthenticationResult.Failed;

            AuthenticationClientData client = _authenticationClients[providerName];
            return _applicationEnvironment.VerifyAuthentication(client.AuthenticationClient, this, returnUrl);
        }

        public bool OAuthLogin(OAuthAccount account, bool remember) {
            AuthenticationClientData oauthProvider = _authenticationClients[account.Provider];
            HttpContextBase context = _applicationEnvironment.AcquireContext();
            var securityManager = new OpenAuthSecurityManager(context, oauthProvider.AuthenticationClient, this);
            bool success = securityManager.Login(account.ProviderUserId, remember);
            if (success)
                return true;

            User user = GetUserByEmailAddress(account.Username);
            if (user == null)
                return false;

            user.OAuthAccounts.Add(account);
            _userCollection.Save(user);

            return securityManager.Login(account.ProviderUserId, remember);
        }

        public static void RegisterClient(IAuthenticationClient client, string displayName, IDictionary<string, object> extraData) {
            var clientData = new AuthenticationClientData(client, displayName, extraData);
            _authenticationClients.Add(client.ProviderName, clientData);
        }

        public User GetUserByEmailAddress(string emailAddress) {
            return _userCollection.FindOne(Query<User>.EQ(u => u.EmailAddress, emailAddress));
        }

        public User Save(User user) {
            _userCollection.Save(user);
            return user;
        }

        public User GetUserByPasswordResetToken(string passwordResetToken) {
            return _userCollection.FindOne(Query<User>.EQ(u => u.PasswordResetToken, passwordResetToken));
        }

        public User GetUserByOAuthProvider(string provider, string providerUserId) {
            return _userCollection.AsQueryable().FirstOrDefault(u => u.OAuthAccounts.Any(r => r.Provider == provider && r.ProviderUserId == providerUserId));
        }

        private static readonly Dictionary<string, AuthenticationClientData> _authenticationClients =
            new Dictionary<string, AuthenticationClientData>(StringComparer.OrdinalIgnoreCase);
    }
}