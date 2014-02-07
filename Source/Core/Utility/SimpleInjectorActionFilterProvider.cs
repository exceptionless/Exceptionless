#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using SimpleInjector;

namespace Exceptionless.Core.Utility {
    public class SimpleInjectorActionFilterProvider : ActionDescriptorFilterProvider, IFilterProvider {
        private readonly Func<Type, Registration> _registrationFactory;

        private readonly ConcurrentDictionary<Type, Registration> _registrations = new ConcurrentDictionary<Type, Registration>();

        public SimpleInjectorActionFilterProvider(Container container) {
            _registrationFactory = concreteType => Lifestyle.Transient.CreateRegistration(concreteType, container);
        }

        public new IEnumerable<FilterInfo> GetFilters(HttpConfiguration configuration, HttpActionDescriptor actionDescriptor) {
            IEnumerable<FilterInfo> filters = base.GetFilters(configuration, actionDescriptor);

            filters = (filters as FilterInfo[]) ?? filters.ToArray();

            foreach (FilterInfo filter in filters) {
                IFilter instance = filter.Instance;

                Registration registration = _registrations.GetOrAdd(instance.GetType(), _registrationFactory);

                registration.InitializeInstance(instance);
            }

            return filters;
        }
    }
}