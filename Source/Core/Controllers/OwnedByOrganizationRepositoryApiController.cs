using System;
using System.Linq;
using System.Web.Http;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Controllers {
    public abstract class OwnedByOrganizationRepositoryApiController<TModel, TViewModel, TRepository> : RepositoryApiController<TModel, TViewModel, TRepository> 
        where TModel : class, IOwnedByOrganization, new() 
        where TViewModel : class, new() 
        where TRepository : MongoRepositoryOwnedByOrganization<TModel> {

        public OwnedByOrganizationRepositoryApiController(TRepository repository) : base(repository) {}

        [Route]
        [HttpGet]
        public override IHttpActionResult Get(int page = 1, int pageSize = 10) {
            var query = Query.In("oid", Request.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id))));
            var results = GetEntities<TViewModel>(query, page: page, pageSize: pageSize);
            return Ok(new PagedResult<TViewModel>(results) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            });
        }

        protected override TModel GetModel(string id) {
            var model = base.GetModel(id);
            if (model == null || !User.IsInOrganization(model.OrganizationId))
                return null;

            return model;
        }

        protected override bool CanAdd(TModel value) {
            return base.CanAdd(value) 
                && !String.IsNullOrEmpty(value.OrganizationId)
                && User.IsInOrganization(value.OrganizationId);
        }

        protected override TModel AddModel(TModel value) {
            // TODO: User can currently specify the organization id to insert.
            //if (String.IsNullOrEmpty(value.OrganizationId))
            //    value.OrganizationId = Request.GetProject().Id;

            return _repository.Add(value);
        }

        protected override bool CanDelete(TModel value) {
            return !User.IsInOrganization(value.OrganizationId);
        }
    }
}