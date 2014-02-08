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
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Mvc;
using Exceptionless.App;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using SimpleInjector;
using SimpleInjector.Integration.Web.Mvc;
using WebActivator;
using IFilterProvider = System.Web.Http.Filters.IFilterProvider;

[assembly: PreApplicationStartMethod(typeof(SimpleInjectorInitializer), "Initialize")]

namespace Exceptionless.App {
    public static class SimpleInjectorInitializer {
        public static void Initialize() {
            Container container = CreateContainer();

            //ServicesContainer services = GlobalConfiguration.Configuration.Services;
            //services.GetHttpControllerTypeResolver().GetControllerTypes(services.GetAssembliesResolver()).Each(container.Register);

            container.Verify();

            RegisterIFilterProvider(GlobalConfiguration.Configuration.Services, container);
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

            return container;
        }

        public static void RegisterIFilterProvider(ServicesContainer services, Container container) {
            services.Remove(typeof(IFilterProvider), GlobalConfiguration.Configuration.Services.GetFilterProviders().OfType<ActionDescriptorFilterProvider>().Single());
            services.Add(typeof(IFilterProvider), new SimpleInjectorActionFilterProvider(container));
        }
    }
}