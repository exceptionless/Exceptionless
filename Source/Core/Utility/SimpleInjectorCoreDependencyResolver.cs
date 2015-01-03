#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Core.Dependency;
using SimpleInjector;

namespace Exceptionless.Core.Utility {
    public class SimpleInjectorCoreDependencyResolver : IDependencyResolver {
        private readonly Container _container;

        public SimpleInjectorCoreDependencyResolver(Container container) {
            _container = container;
        }

        public object GetService(Type serviceType) {
            return _container.GetInstance(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return _container.GetAllInstances(serviceType);
        }
    }
}