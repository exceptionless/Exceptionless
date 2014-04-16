#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.Core.Extensions {
    public static class ContainerExtensions {
        public static void RegisterPackage<TPackage>(this Container container) {
            if (container == null)
                throw new ArgumentNullException("container");

            var package = Activator.CreateInstance(typeof(TPackage)) as IPackage;
            if (package == null)
                throw new ArgumentException("TPackage must implement IPackage.");

            package.RegisterServices(container);
        }

        public static void RegisterSingleImplementation<TImplementation>(this Container container, params Type[] serviceTypesToRegisterFor) {
            var implementationType = typeof(TImplementation);
            var registration = Lifestyle.Singleton.CreateRegistration(implementationType, implementationType, container);
            foreach (var serviceType in serviceTypesToRegisterFor)
                container.AddRegistration(serviceType, registration);
        }
    }
}