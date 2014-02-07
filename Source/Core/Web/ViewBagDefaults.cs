#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Web.Mvc;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using SimpleInjector;

namespace Exceptionless.Core.Web {
    public class ViewBagDefaults : FilterAttribute, IResultFilter {
        [Inject]
        public Container Container { get; set; }

        public void OnResultExecuting(ResultExecutingContext context) {
            var user = context.HttpContext.User as ExceptionlessPrincipal;
            if (user == null || user.UserEntity == null)
                return;

            context.Controller.ViewBag.User = user.UserEntity;

            var organizationRepository = Container.GetInstance<IOrganizationRepository>();
            if (organizationRepository == null)
                return;

            if (user.UserEntity.OrganizationIds.Count == 0)
                return;

            // TODO: We need to be using the organization from whichever project they are viewing.
            Organization organization = organizationRepository.GetByIdCached(user.UserEntity.OrganizationIds.First());
            context.Controller.ViewBag.IntercomData = new IntercomModel(user.UserEntity, organization);
        }

        public void OnResultExecuted(ResultExecutedContext context) {}
    }
}