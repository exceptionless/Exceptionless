#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.ComponentModel.DataAnnotations;
using Exceptionless.App.Models.Common;

namespace Exceptionless.App.Models.Account {
    public class ResetPasswordModel : NewPasswordModel {
        [Required]
        [Display(Name = "Reset password token")]
        public string ResetPasswordToken { get; set; }

        public bool Cancelled { get; set; }
    }
}