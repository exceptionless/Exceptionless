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
using DataAnnotationsExtensions;

namespace Exceptionless.App.Models.Common {
    public class EmailAddressModel {
        [Email]
        [Required]
        //[Remote("IsEmailAddressAvaliable", "Account", ErrorMessage = "A user already exists with this email address.")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email")]
        public string EmailAddress { get; set; }
    }
}