#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.App.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Membership;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.App {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            container.Options.AllowOverridingRegistrations = true;
            container.RegisterPackage<Core.Bootstrapper>();

            container.RegisterSingle<IMembershipSecurity, DefaultMembershipSecurity>();
            container.RegisterSingle<IMembershipProvider, MembershipProvider>();

            if (Settings.Current.EnableJobsModule)
                DynamicModuleUtility.RegisterModule(typeof(ExceptionlessJobHttpModule));
        }
    }
}