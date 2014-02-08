#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.App.Models.User {
    public class UserModel : Common.UserModelBase {
        public string Id { get; set; }
        public bool IsEmailAddressVerified { get; set; }
        public bool IsInvite { get; set; }
        public bool HasAdminRole { get; set; }
    }
}