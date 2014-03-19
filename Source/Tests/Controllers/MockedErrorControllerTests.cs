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
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using Exceptionless.App.Controllers.API;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Models;
using Exceptionless.Tests.Controllers.Base;
using Exceptionless.Tests.Utility;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Controllers {
    public class MockedErrorControllerTests : MockedApiControllerBase<ErrorController, IErrorRepository, Error> {
        private Mock<IProjectRepository> _mockedProjectRepository;

        private Mock<IOrganizationRepository> _mockedOrganizationRepository;

        public MockedErrorControllerTests() : base(true) {}

        [Fact]
        public void Get() {
            IEnumerable<Error> result = _controller.Get();
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetById() {
            Error result = _controller.Get(TestConstants.ErrorId);
            Assert.NotNull(result);
            Assert.Equal(TestConstants.ErrorId, result.Id);
        }

        [Fact]
        public void Post() {
            HttpResponseMessage result = _controller.Post(ErrorData.GenerateSampleError(TestConstants.ErrorId3));
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);

            IEnumerable<Error> errors = _controller.Get();
            Assert.NotNull(errors);
            Assert.Equal(2, errors.Count());
        }

        [Fact]
        public void Put() {
            HttpResponseMessage result = _controller.Put(TestConstants.ErrorId, new Core.Web.OData.Delta<Error>());
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);

            var delta = new Core.Web.OData.Delta<Error>();
            Assert.True(delta.TrySetPropertyValue("UserName", "Tester"));

            result = _controller.Put(TestConstants.ErrorId, delta);
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void Patch() {
            HttpResponseMessage result = _controller.Patch(TestConstants.ErrorId, new Core.Web.OData.Delta<Error>());
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);

            var delta = new Core.Web.OData.Delta<Error>();
            Assert.True(delta.TrySetPropertyValue("UserName", "Tester"));

            result = _controller.Patch(TestConstants.ErrorId, delta);
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void Delete() {
            Error result = null;
            Exception exception = Record.Exception(() => result = _controller.Get(TestConstants.ErrorId2));
            Assert.Null(exception);

            HttpResponseMessage response = _controller.Delete(TestConstants.ErrorId2);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

            //if (result == null)
            //    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            //else
            //    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            //// Ensure it was deleted.
            //exception = Record.Exception(() => _controller.Get(TestConstants.ErrorId2));
            //Assert.NotNull(exception);
        }

        [Fact]
        public void Batch() {
            // TODO: Add batch unit tests.
        }

        protected override ErrorController CreateController() {
            _mockedProjectRepository = new Mock<IProjectRepository>();
            _mockedOrganizationRepository = new Mock<IOrganizationRepository>();

            return (ErrorController)Activator.CreateInstance(typeof(ErrorController), _repository.Object, _mockedOrganizationRepository.Object, _mockedProjectRepository.Object, null, null, new NullAppStatsClient());
        }

        protected override void CreateData() {
            _data = new List<Error> {
                ErrorData.GenerateSampleError(),
                ErrorData.GenerateSampleError(TestConstants.ErrorId2)
            };
        }

        protected override void RemoveData() {}

        protected override void SetUp() {
            base.SetUp();

            _repository.Setup(r => r.All()).Returns(_data.AsQueryable);
            _repository.Setup(r => r.GetByOrganizationIds(It.IsAny<IEnumerable<String>>())).Returns((IEnumerable<string> ids) => _data.Where(d => ids.Contains(d.OrganizationId)).AsQueryable());
            _repository.Setup(r => r.GetById(It.IsAny<String>(), It.IsAny<bool>())).Returns<string>(id => _data.FirstOrDefault(d => String.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)));
            _repository.Setup(r => r.FirstOrDefault(It.IsAny<Expression<Func<Error, bool>>>())).Returns<Error>(e => _data.FirstOrDefault(d => String.Equals(d.Id, e.Id, StringComparison.OrdinalIgnoreCase)));
            _repository.Setup(r => r.Where(It.IsAny<Expression<Func<Error, bool>>>())).Returns(_data.AsQueryable());
            _repository.Setup(r => r.Where(It.IsAny<IMongoQuery>())).Returns(_data.AsQueryable());
            _repository.Setup(r => r.Delete(It.IsAny<IMongoQuery>())).Callback<IMongoQuery>(id => _data.RemoveAll(d => String.Equals(d.Id, TestConstants.ErrorId2, StringComparison.OrdinalIgnoreCase)));
            _repository.Setup(r => r.Delete(It.IsAny<String>())).Callback<string>(id => _data.RemoveAll(d => String.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)));
            _repository.Setup(r => r.Add(It.IsAny<Error>(), It.IsAny<bool>())).Callback<Error>(e => _data.Add(e));
            _repository.Setup(r => r.Count()).Returns<int>(e => _data.Count);
            _repository.Setup(r => r.Update(It.IsAny<Error>(), It.IsAny<bool>())).Callback<Error>(e => {
                _data.RemoveAll(er => er.Id == e.Id);
                _data.Add(e);
            });
        }
    }
}