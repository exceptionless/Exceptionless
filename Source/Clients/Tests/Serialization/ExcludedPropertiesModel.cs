#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Tests.Serialization {
    public class ExcludedPropertiesModel {
        public string CardFullName { get; set; }
        public string CardNumber { get; set; }
        public string CardLastFour { get; set; }
        public string CardType { get; set; }
        public DateTime Expiration { get; set; }

        public string UserName { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string PasswordSalt { get; set; }
        public string HashCode { get; set; }
        public bool ResetPasswordAfter90Days { get; set; }
        public bool RememberMe { get; set; }

        public string SSN { get; set; }
        public string SocialSecurityNumber { get; set; }

        public string PhoneNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string EncryptedString { get; set; }
    }
}