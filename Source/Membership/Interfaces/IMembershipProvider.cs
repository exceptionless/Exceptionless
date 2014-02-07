#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Membership {
    public interface IMembershipProvider : IOAuthProvider {
        bool Login(string emailAddress, string password, bool remember = false);

        void Logout();

        void CreateAccount(User user);

        void UpdateAccount(User user);

        User GetUserByEmailAddress(string emailAddress);

        bool HasLocalAccount(string emailAddress);

        string GenerateVerifyEmailToken(string emailAddress, int tokenExpirationInMinutesFromNow = 1440);

        bool VerifyEmailAddress(string verifyEmailToken);

        bool ChangePassword(string emailAddress, string oldPassword, string newPassword);

        void SetLocalPassword(string emailAddress, string newPassword);

        string GeneratePasswordResetToken(string emailAddress, int tokenExpirationInMinutesFromNow = 1440);

        bool CancelPasswordReset(string passwordResetToken);

        bool ResetPassword(string passwordResetToken, string newPassword);
    }
}