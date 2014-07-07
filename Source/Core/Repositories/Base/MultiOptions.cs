using System;
using CodeSmith.Core.Events;
using Exceptionless.Core.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MultiOptions : OneOptions {
        public event EventHandler<EventArgs<bool>> HasMoreChanged;
        private bool _hasMore;

        public bool HasMore {
            get { return _hasMore; }
            set {
                _hasMore = value;
                if (HasMoreChanged != null)
                    HasMoreChanged(this, new EventArgs<bool>(_hasMore));
            }
        }

        public string BeforeValue { get; set; }
        public IMongoQuery BeforeQuery { get; set; }
        public string AfterValue { get; set; }
        public IMongoQuery AfterQuery { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }

        public bool UseLimit {
            get { return Limit.HasValue; }
        }

        public bool UseSkip {
            get { return UsePaging; }
        }

        public bool UsePaging {
            get { return Page.HasValue; }
        }

        public int GetLimit() {
            if (!Limit.HasValue || Limit.Value < 1)
                return RepositoryConstants.DEFAULT_LIMIT;

            if (Limit.Value > RepositoryConstants.MAX_LIMIT)
                return RepositoryConstants.MAX_LIMIT;

            return Limit.Value;
        }

        public int GetSkip() {
            if (!Page.HasValue || Page.Value < 1)
                return 0;

            int skip = (Page.Value - 1) * GetLimit();
            if (skip < 0)
                skip = 0;

            return skip;
        }

        public override IMongoQuery GetQuery(Func<string, BsonValue> getIdValue = null) {
            IMongoQuery query = base.GetQuery(getIdValue);
            if (getIdValue == null)
                getIdValue = id => new BsonObjectId(new ObjectId(id));

            if (Page.HasValue)
                return query;

            if (!String.IsNullOrEmpty(BeforeValue) && BeforeQuery == null) {
                try {
                    BeforeQuery = MongoDB.Driver.Builders.Query.LT(CommonFieldNames.Id, getIdValue(BeforeValue));
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(BeforeQuery, "BeforeQuery").Submit();
                }
            }

            if (!String.IsNullOrEmpty(AfterValue) && AfterQuery == null) {
                try {
                    AfterQuery = MongoDB.Driver.Builders.Query.GT(CommonFieldNames.Id, getIdValue(AfterValue));
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(AfterQuery, "AfterQuery").Submit();
                }
            }

            query = query.And(BeforeQuery, AfterQuery);
            return query;
        }
    }
}