#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Core.Extensions {
    public static class UserExtensions {
        public static void CreateVerifyEmailAddressToken(this User user) {
            if (user == null)
                return;

            user.VerifyEmailAddressToken = StringExtensions.GetNewToken();
            user.VerifyEmailAddressTokenExpiration = DateTime.UtcNow.AddMinutes(1440);
        }

        public static bool HasValidEmailAddressTokenExpiration(this User user) {
            if (user == null)
                return false;

            return user.VerifyEmailAddressTokenExpiration != DateTime.MinValue && user.VerifyEmailAddressTokenExpiration >= DateTime.UtcNow;
        }

        public static void MarkEmailAddressVerified(this User user) {
            if (user == null)
                return;

            user.IsEmailAddressVerified = true;
            user.VerifyEmailAddressToken = null;
            user.VerifyEmailAddressTokenExpiration = DateTime.MinValue;
        }

        public static void ResetPasswordResetToken(this User user) {
            if (user == null)
                return;

            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiration = DateTime.MinValue;
        }
    }
}