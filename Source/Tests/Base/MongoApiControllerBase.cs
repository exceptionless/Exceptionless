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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Exceptionless.App;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web;
using SimpleInjector;

namespace Exceptionless.Tests.Controllers.Base {
    public abstract class MongoApiControllerBase<TModel, TResponseModel, TRepository> : MongoRepositoryTestBase<TModel, TRepository>
        where TModel : class, new()
        where TResponseModel : class
        where TRepository : class, IRepository<TModel> {
        protected readonly string _baseUrl;
        private HttpServer _server;

        protected MongoApiControllerBase(TRepository repository, bool tearDownOnExit) : base(repository, tearDownOnExit) {
            string type = typeof(TModel).Name.ToLower();
            _baseUrl = String.Concat(Settings.Current.BaseURL, "/api/v1/", type);
        }

        protected TModel GetResponse(string id) {
            return CreateResponse<TModel>(id: id);
        }

        protected IEnumerable<TModel> GetAllResponse() {
            return CreateResponse<IEnumerable<TModel>>();
        }

        protected TResponseModel PostResponse(TModel value) {
            return CreateResponse<TResponseModel>(HttpVerbs.Post, value);
        }

        protected TResponseModel PutResponse(string id, TModel value) {
            return CreateResponse<TResponseModel>(HttpVerbs.Put, value, id);
        }

        protected TResponseModel PatchResponse(string id, Object value) {
            return CreateResponse<TResponseModel>(HttpVerbs.Patch, value, id);
        }

        protected TResponseModel DeleteResponse(string id) {
            return CreateResponse<TResponseModel>(HttpVerbs.Delete, id: id);
        }

        protected R CreateResponse<R>(HttpVerbs verb = HttpVerbs.Get, Object value = default(TModel), string id = null, Uri uri = null) where R : class {
            HttpClient client = CreateClient(_server, verb);

            if (uri == null)
                uri = GetUri(verb, id);

            Task<HttpResponseMessage> task = null;
            switch (verb) {
                case HttpVerbs.Delete:
                    task = client.DeleteAsync(uri);
                    break;
                case HttpVerbs.Get:
                    task = client.GetAsync(uri);
                    break;
                case HttpVerbs.Post:
                    task = client.PostAsJsonAsync(uri.ToString(), (TModel)value);
                    break;
                case HttpVerbs.Put:
                    task = client.PutAsJsonAsync(uri.ToString(), (TModel)value);
                    break;
                case HttpVerbs.Patch:
                    task = client.PatchAsJsonAsync(uri.ToString(), value);
                    break;
                case HttpVerbs.Head:
                    break;
                case HttpVerbs.Options:
                    break;
            }

            if (task == null || task.Result == null)
                return null;

            if (!task.Result.IsSuccessStatusCode) {
                Console.WriteLine(task.Result.ToString());

                HttpError error;
                if (task.Result.TryGetContentValue(out error))
                    Console.WriteLine(error.GetAllMessages(true));
            }

            OnResponseCreated(task.Result);

            if (task.Result is R)
                return task.Result as R;

            return task.Result.StatusCode == HttpStatusCode.OK ? task.Result.Content.ReadAsAsync<R>().Result : null;
        }

        protected virtual void OnResponseCreated(HttpResponseMessage response) {}

        protected virtual HttpConfiguration CreateConfiguration() {
            var config = new HttpConfiguration {
                IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always
            };

            SimpleInjectorInitializer.RegisterIFilterProvider(config.Services, IoC.GetInstance<Container>());

            WebApiConfig.Register(config);

            config.DependencyResolver = GlobalConfiguration.Configuration.DependencyResolver;

            return config;
        }

        protected virtual HttpClient CreateClient(HttpServer server, HttpVerbs verb) {
            return new HttpClient(server);
        }

        protected virtual Uri GetUri(HttpVerbs verb, string id) {
            return String.IsNullOrEmpty(id) ? new Uri(_baseUrl) : new Uri(String.Format("{0}/{1}", _baseUrl, id));
        }

        protected override void SetUp() {
            base.SetUp();

            _server = new HttpServer(CreateConfiguration());
        }

        protected override void TearDown() {
            base.TearDown();

            if (_server == null)
                return;

            _server.Dispose();
            _server = null;
        }
    }
}