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
using System.Diagnostics;
using System.Web.Http.Dependencies;
using SimpleInjector;

namespace Exceptionless.Web.App_Start {
    public sealed class SimpleInjectorWebApiDependencyResolver : IDependencyResolver {
        private readonly Container _container;

        public SimpleInjectorWebApiDependencyResolver(Container container) {
            _container = container;
        }

        [DebuggerStepThrough]
        public IDependencyScope BeginScope() {
            return this;
        }

        [DebuggerStepThrough]
        public object GetService(Type serviceType) {
            return ((IServiceProvider)_container).GetService(serviceType);
        }

        [DebuggerStepThrough]
        public IEnumerable<object> GetServices(Type serviceType) {
            return _container.GetAllInstances(serviceType);
        }

        [DebuggerStepThrough]
        public void Dispose() {}
    }
}