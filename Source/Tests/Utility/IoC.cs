#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Http;
using Exceptionless.App;
using Exceptionless.Core.Mail;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;

namespace Exceptionless.Tests.Utility {
    public static class IoC {
        private static void RegisterServices(Container container) {
            container.Register<IMailer, NullMailer>();
        }

        #region Bootstrap

        private static Container _container;
        private static bool _initialized = false;

        public static TService GetInstance<TService>() where TService : class {
            if (!_initialized)
                Initialize();

            object result = _container.GetInstance(typeof(TService));
            return result as TService;
        }

        private static Container GetContainer() {
            if (!_initialized)
                Initialize();

            return _container;
        }

        private static void Initialize() {
            _container = SimpleInjectorInitializer.CreateContainer();
            _initialized = true;

            RegisterServices(_container);

            GlobalConfiguration.Configuration.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(_container);
        }

        #endregion
    }
}