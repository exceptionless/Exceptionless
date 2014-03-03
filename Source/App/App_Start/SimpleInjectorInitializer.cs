#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Reflection;
using System.Web.Http;
using System.Web.Mvc;
using Exceptionless.App;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using SimpleInjector;
using SimpleInjector.Integration.Web.Mvc;
using SimpleInjector.Integration.WebApi;
using WebActivator;

[assembly: PreApplicationStartMethod(typeof(SimpleInjectorInitializer), "Initialize")]

namespace Exceptionless.App {
    public static class SimpleInjectorInitializer {
        public static void Initialize() {
            Container container = CreateContainer();

            //ServicesContainer services = GlobalConfiguration.Configuration.Services;
            //services.GetHttpControllerTypeResolver().GetControllerTypes(services.GetAssembliesResolver()).Each(container.Register);

            container.Verify();

            GlobalConfiguration.Configuration.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);
            DependencyResolver.SetResolver(new SimpleInjectorDependencyResolver(container));
        }

        public static Container CreateContainer() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();

            container.RegisterMvcControllers(Assembly.GetExecutingAssembly());
            container.RegisterMvcAttributeFilterProvider();

            //container.RegisterWebApiControllers(GlobalConfiguration.Configuration);
            container.RegisterWebApiFilterProvider(GlobalConfiguration.Configuration);

            return container;
        }
    }
}