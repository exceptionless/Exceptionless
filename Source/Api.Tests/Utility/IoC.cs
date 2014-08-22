#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core.Mail;
using Nest;
using SimpleInjector;

namespace Exceptionless.Api.Tests.Utility {
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

        public static Container GetContainer() {
            if (!_initialized)
                Initialize();

            return _container;
        }

        private static void Initialize() {
            _container = AppBuilder.CreateContainer();
            _initialized = true;

            RegisterServices(_container);

            // TODO: We need to figure out how to be able to run unit tests separate from our normal data.
            var searchclient = _container.GetInstance<IElasticClient>();
            searchclient.DeleteIndex(i => i.AllIndices());
        }

        #endregion
    }
}