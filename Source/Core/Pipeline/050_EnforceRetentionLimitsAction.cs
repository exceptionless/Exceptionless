#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Pipeline {
    [Priority(50)]
    public class EnforceRetentionLimitsAction : ErrorPipelineActionBase {
        private readonly ErrorRepository _errorRepository;
        private readonly OrganizationRepository _organizationRepository;

        public EnforceRetentionLimitsAction(ErrorRepository errorRepository, OrganizationRepository organizationRepository) {
            _errorRepository = errorRepository;
            _organizationRepository = organizationRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            if (ctx.IsNew)
                return;

            int maxErrorsPerStack = 50;
            Organization organization = _organizationRepository.GetByIdCached(ctx.Error.OrganizationId);
            if (organization != null)
                maxErrorsPerStack = organization.MaxErrorsPerDay > 0 ? organization.MaxErrorsPerDay + Math.Min(50, organization.MaxErrorsPerDay * 2) : Int32.MaxValue;

            // get a list of oldest ids that exceed our desired max errors
            var errors = _errorRepository.Collection.Find(Query.EQ(ErrorRepository.FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(ctx.Error.ErrorStackId))))
                .SetSortOrder(SortBy.Descending(ErrorRepository.FieldNames.OccurrenceDate_UTC))
                .SetFields(ErrorRepository.FieldNames.Id)
                .Select(e => new Error {
                    Id = e.Id,
                    OrganizationId = ctx.Error.OrganizationId,
                    ProjectId = ctx.Error.ProjectId,
                    ErrorStackId = ctx.Error.ErrorStackId
                })
                .Skip(maxErrorsPerStack)
                .Take(150).ToArray();

            if (errors.Length > 0)
                _errorRepository.Delete(errors);
        }
    }
}