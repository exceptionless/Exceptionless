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
using System.Web.Mvc;
using DataAnnotationsExtensions;

namespace Exceptionless.Web.Models.Account {
    public class ManageModel : LocalPasswordModel {
        [Required(ErrorMessage = "Full name is required.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; }

        [Email]
        [Required]
        [Remote("IsEmailAddressAvaliable", "Account", ErrorMessage = "A user already exists with this email address.")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; }

        [Display(Name = "Enable email notifications")]
        public bool EmailNotificationsEnabled { get; set; }
    }
}