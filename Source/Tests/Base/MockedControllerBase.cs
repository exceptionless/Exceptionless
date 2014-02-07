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
using System.Threading;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Controllers;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Moq;

namespace Exceptionless.Tests.Controllers.Base {
    public abstract class MockedApiControllerBase<TController, TRepository, TModel> : DataTestBase
        where TController : RepositoryBaseApiController<TModel>
        where TRepository : class, IRepositoryWithIdentity<TModel>
        where TModel : class, IOwnedByOrganization, new() {
        protected readonly TController _controller;
        protected readonly Mock<TRepository> _repository;
        protected List<TModel> _data;

        protected MockedApiControllerBase(bool tearDownOnExit) : base(tearDownOnExit) {
            _repository = new Mock<TRepository>();
            Thread.CurrentPrincipal = new ExceptionlessPrincipal(UserData.GenerateSampleUser());
            _controller = CreateController();

            Reset();
        }

        protected virtual TController CreateController() {
            return (TController)Activator.CreateInstance(typeof(TController), _repository.Object);
        }
    }
}