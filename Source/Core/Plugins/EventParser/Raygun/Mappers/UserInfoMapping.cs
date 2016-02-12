using System;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class UserInfoMapping {
        public static UserInfo Map(RaygunModel model) {
            var user = model?.Details?.User;
            if (user == null)
                return null;

            var ui = new UserInfo {
                Identity = user.Email,
                Name = user.FullName
            };

            // NOTE: We try and set the users id to email in our system (and index it as an email address). Should we set it to the user id? 
            ui.Data[nameof(user.Email)] = user.Email;
            ui.Data[nameof(user.FirstName)] = user.FirstName;
            ui.Data[nameof(user.FullName)] = user.FullName;
            ui.Data[nameof(user.Identifier)] = user.Identifier;
            ui.Data[nameof(user.IsAnonymous)] = user.IsAnonymous;
            ui.Data[nameof(user.Uuid)] = user.Uuid;

            return ui;
        }
    }
}