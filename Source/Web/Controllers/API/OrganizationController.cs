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
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Web;
using Exceptionless.Core.Web.OData;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models.Organization;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog.Fluent;
using ServiceStack.CacheAccess;
using Stripe;

namespace Exceptionless.Web.Controllers.Service {
    [ConfigurationResponseFilter]
    [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : RepositoryApiController<Organization, IOrganizationRepository> {
        private readonly ICacheClient _cacheClient;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;
        private readonly ProjectController _projectController;
        private readonly IMailer _mailer;

        public OrganizationController(IOrganizationRepository repository, IUserRepository userRepository, IProjectRepository projectRepository, BillingManager billingManager, NotificationSender notificationSender, ICacheClient cacheClient, ProjectController projectController, IMailer mailer)
            : base(repository) {
            _cacheClient = cacheClient;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _projectController = projectController;
            _mailer = mailer;
        }

        public override IEnumerable<Organization> Get() {
            return _repository.GetByIds(User.GetAssociatedOrganizationIds());
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public PagedResult<Organization> List(string criteria = null, bool? isPaidPlan = null, bool? isSuspended = null, OrganizationSortBy sortBy = OrganizationSortBy.Newest, int page = 1, int pageSize = 10) {
            int skip = (page - 1) * pageSize;
            if (skip < 0)
                skip = 0;

            if (pageSize < 1)
                pageSize = 10;

            var queries = new List<IMongoQuery>();
            if (!String.IsNullOrWhiteSpace(criteria))
                queries.Add(Query.Matches(OrganizationRepository.FieldNames.Name, new BsonRegularExpression(String.Format("/{0}/i", criteria))));

            if (isPaidPlan.HasValue) {
                if (isPaidPlan.Value)
                    queries.Add(Query.NE(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
                else
                    queries.Add(Query.EQ(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
            }

            if (isSuspended.HasValue) {
                if (isSuspended.Value)
                    queries.Add(
                        Query.Or(
                            Query.And(
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)), 
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing))), 
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(true))));
                else
                    queries.Add(Query.And(
                            Query.Or(
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)),
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing))),
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(false))));
            }

            SortByBuilder sort;
            if (sortBy == OrganizationSortBy.Newest)
                sort = SortBy.Descending(OrganizationRepository.FieldNames.Id);
            else if (sortBy == OrganizationSortBy.MostActive)
                sort = SortBy.Descending(OrganizationRepository.FieldNames.TotalErrorCount);
            else
                sort = SortBy.Ascending(OrganizationRepository.FieldNames.Name);

            MongoCursor<Organization> query = queries.Count > 0 ? ((OrganizationRepository)_repository).Collection.Find(Query.And(queries)) : ((OrganizationRepository)_repository).Collection.FindAll();
            List<Organization> results = query.SetSortOrder(sort).SetSkip(skip).SetLimit(pageSize).ToList();
            return new PagedResult<Organization>(results) {
                Page = page,
                PageSize = pageSize,
                TotalCount = query.Count()
            };
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.User)]
        public PagedResult<InvoiceGridModel> Payments(string id, int page = 1, int pageSize = 12) {
            if (String.IsNullOrWhiteSpace(id) || !User.CanAccessOrganization(id))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            int skip = (page - 1) * pageSize;
            if (skip < 0)
                skip = 0;

            if (pageSize < 1)
                pageSize = 10;

            Organization organization = _repository.GetByIdCached(id);
            if (organization == null || String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return new PagedResult<InvoiceGridModel>();

            var invoiceService = new StripeInvoiceService();
            List<InvoiceGridModel> invoices = invoiceService.List(pageSize, skip, organization.StripeCustomerId).Select(Mapper.Map<InvoiceGridModel>).ToList();
            return new PagedResult<InvoiceGridModel>(invoices) {
                Page = page,
                PageSize = pageSize,
                TotalCount = invoices.Count // TODO: Return the total count.
            };
        }

        protected override Organization GetEntity(string id) {
            Organization entity = _repository.GetById(id);
            return entity != null && User.CanAccessOrganization(entity.Id) ? entity : null;
        }

        protected override Organization InsertEntity(Organization value) {
            if (String.IsNullOrEmpty(value.Name))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            if (!IsNameAvailable(value.Name))
                throw new HttpResponseException(DuplicateResponseMessage(value.Id));

            if (!_billingManager.CanAddOrganization(User.UserEntity))
                throw new HttpResponseException(PlanLimitReached("Please upgrade your plan to add an additional organization."));

            _billingManager.ApplyBillingPlan(value, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, User.UserEntity);

            value.ProjectCount = 0;
            value.StackCount = 0;
            value.ErrorCount = 0;
            value.TotalErrorCount = 0;

            // TODO: User can currently specify the organization id to insert.
            Organization organization = base.InsertEntity(value);

            User user = _userRepository.GetById(User.UserEntity.Id);
            user.OrganizationIds.Add(organization.Id);
            _userRepository.Update(user);

            _notificationSender.OrganizationUpdated(organization.Id);

            return organization;
        }

        protected override bool CanUpdateEntity(Organization original, Delta<Organization> value) {
            Organization organization = value.GetEntity();
            if (value.ContainsChangedProperty(t => t.Id)
                && !String.Equals(original.Id, organization.Id, StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.ContainsChangedProperty(t => t.Name)
                && !String.Equals(original.Name, organization.Name, StringComparison.OrdinalIgnoreCase)
                && !IsNameAvailable(organization.Name))
                return false;

            if ((value.ContainsChangedProperty(t => t.MaxErrorsPerDay) && original.MaxErrorsPerDay != organization.MaxErrorsPerDay)
                || (value.ContainsChangedProperty(t => t.LastErrorDate) && original.LastErrorDate != organization.LastErrorDate)
                || (value.ContainsChangedProperty(t => t.StripeCustomerId) && original.StripeCustomerId != organization.StripeCustomerId)
                || (value.ContainsChangedProperty(t => t.PlanId) && original.PlanId != organization.PlanId)
                || (value.ContainsChangedProperty(t => t.CardLast4) && original.CardLast4 != organization.CardLast4)
                || (value.ContainsChangedProperty(t => t.SubscribeDate) && original.SubscribeDate != organization.SubscribeDate)
                || (value.ContainsChangedProperty(t => t.BillingChangeDate) && original.BillingChangeDate != organization.BillingChangeDate)
                || (value.ContainsChangedProperty(t => t.RetentionDays) && original.RetentionDays != organization.RetentionDays)
                || (value.ContainsChangedProperty(t => t.MaxErrorsPerDay) && original.MaxErrorsPerDay != organization.MaxErrorsPerDay)
                || (value.ContainsChangedProperty(t => t.MaxProjects) && original.MaxProjects != organization.MaxProjects)
                || (value.ContainsChangedProperty(t => t.ProjectCount) && original.ProjectCount != organization.ProjectCount)
                || (value.ContainsChangedProperty(t => t.StackCount) && original.StackCount != organization.StackCount)
                || (value.ContainsChangedProperty(t => t.ErrorCount) && original.ErrorCount != organization.ErrorCount)
                || (value.ContainsChangedProperty(t => t.TotalErrorCount) && original.TotalErrorCount != organization.TotalErrorCount)
                || (value.ContainsChangedProperty(t => t.SuspensionDate) && original.SuspensionDate != organization.SuspensionDate)
                || (value.ContainsChangedProperty(t => t.SuspendedByUserId) && original.SuspendedByUserId != organization.SuspendedByUserId))
                return false;

            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin)
                && ((value.ContainsChangedProperty(t => t.IsSuspended) && original.IsSuspended != organization.IsSuspended)
                    || (value.ContainsChangedProperty(t => t.SuspensionCode) && original.SuspensionCode != organization.SuspensionCode)
                    || (value.ContainsChangedProperty(t => t.SuspensionNotes) && original.SuspensionNotes != organization.SuspensionNotes)))
                return false;

            return base.CanUpdateEntity(original, value);
        }

        protected override Organization UpdateEntity(Organization original, Delta<Organization> value) {
            if (String.IsNullOrWhiteSpace(original.Id) || !User.CanAccessOrganization(original.Id))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            Organization organization = value.GetEntity();
            bool suspendedStateChanged = (value.ContainsChangedProperty(t => t.IsSuspended) && original.IsSuspended != organization.IsSuspended)
                                         || (value.ContainsChangedProperty(t => t.SuspensionCode) && original.SuspensionCode != organization.SuspensionCode)
                                         || (value.ContainsChangedProperty(t => t.SuspensionNotes) && original.SuspensionNotes != organization.SuspensionNotes);

            value.Patch(original);

            if (suspendedStateChanged) {
                if (original.IsSuspended) {
                    original.SuspensionDate = DateTime.Now;
                    original.SuspendedByUserId = User.UserEntity != null ? User.UserEntity.Id : null;
                    if (!String.Equals(original.SuspensionCode, SuspensionCodes.Abuse) && !String.Equals(original.SuspensionCode, SuspensionCodes.Billing) && !String.Equals(original.SuspensionCode, SuspensionCodes.Overage))
                        original.SuspensionCode = SuspensionCodes.Other;
                } else {
                    original.SuspensionDate = null;
                    original.SuspensionCode = null;
                    original.SuspensionNotes = null;
                    original.SuspendedByUserId = null;
                }
            }

            organization = _repository.Update(original);
            _notificationSender.OrganizationUpdated(organization.Id);

            return organization;
        }

        protected override void DeleteEntity(Organization value) {
            if (String.IsNullOrWhiteSpace(value.Id) || !User.CanAccessOrganization(value.Id))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            if (!String.IsNullOrEmpty(value.StripeCustomerId) && User.IsInRole(AuthorizationRoles.GlobalAdmin))
                throw new HttpResponseException(BadRequestErrorResponseMessage("An organization cannot be deleted if it has a subscription."));

            List<Project> projects = _projectRepository.GetByOrganizationId(value.Id).ToList();
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Any())
                throw new HttpResponseException(BadRequestErrorResponseMessage("An organization cannot be deleted if it contains any projects."));

            Log.Info().Message("User {0} deleting organization {1} with {2} errors.", User.UserEntity.Id, value.Id, value.ErrorCount).Write();

            if (!String.IsNullOrEmpty(value.StripeCustomerId)) {
                Log.Info().Message("Canceling stripe subscription for the organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();

                var customerService = new StripeCustomerService();
                customerService.CancelSubscription(value.StripeCustomerId);
            }

            List<User> users = _userRepository.GetByOrganizationId(value.Id).ToList();
            foreach (User user in users) {
                // delete the user if they are not associated to any other organizations and they are not the current user
                if (user.OrganizationIds.All(oid => String.Equals(oid, value.Id)) && !String.Equals(user.Id, User.UserEntity.Id)) {
                    Log.Info().Message("Removing user '{0}' as they do not belong to any other organizations.", user.Id, value.Name, value.Id).Write();
                    _userRepository.Delete(user.Id);
                } else {
                    Log.Info().Message("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, value.Name, value.Id).Write();
                    user.OrganizationIds.Remove(value.Id);
                    _userRepository.Update(user);
                }
            }

            if (User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Count > 0) {
                foreach (Project project in projects) {
                    Log.Info().Message("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id).Write();
                    _projectController.ResetData(project.Id);
                }

                Log.Info().Message("Deleting all projects for organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
                _projectRepository.Delete(projects);
            }

            Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
            base.DeleteEntity(value);

            _notificationSender.OrganizationUpdated(value.Id);
        }

        [HttpPost]
        public User Invite(string id, string emailAddress) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(emailAddress))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            Organization organization = GetEntity(id);
            if (organization == null)
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            if (!_billingManager.CanAddUser(organization))
                throw new HttpResponseException(PlanLimitReached("Please upgrade your plan to add an additional user."));

            User user = _userRepository.GetByEmailAddress(emailAddress);
            if (user != null) {
                if (!user.OrganizationIds.Contains(organization.Id)) {
                    user.OrganizationIds.Add(organization.Id);
                    _userRepository.Update(user);
                }

                _mailer.SendAddedToOrganizationAsync(User.UserEntity, organization, user);
            } else {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase));
                if (invite == null) {
                    invite = new Invite {
                        Token = Guid.NewGuid().ToString("N").ToLower(),
                        EmailAddress = emailAddress,
                        DateAdded = DateTime.UtcNow
                    };
                    organization.Invites.Add(invite);
                    _repository.Update(organization);
                }

                _mailer.SendInviteAsync(User.UserEntity, organization, invite);
            }

            _notificationSender.OrganizationUpdated(organization.Id);
            return user != null ? new User { EmailAddress = user.EmailAddress } : null;
        }

        [HttpDelete]
        public HttpResponseMessage RemoveUser(string id, string emailAddress) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(emailAddress))
                return BadRequestErrorResponseMessage();

            Organization organization = GetEntity(id);
            if (organization == null)
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            User user = _userRepository.GetByEmailAddress(emailAddress); // TODO This should be by email address.
            if (user == null || !user.OrganizationIds.Contains(id)) {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase));
                if (invite != null) {
                    organization.Invites.Remove(invite);
                    _repository.Update(organization);
                }
            } else {
                if (!user.OrganizationIds.Contains(organization.Id))
                    throw new HttpResponseException(BadRequestErrorResponseMessage());

                if (_userRepository.GetByOrganizationId(organization.Id).Count() == 1)
                    throw new HttpResponseException(BadRequestErrorResponseMessage("An organization must contain at least one user."));

                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).Where(p => p.NotificationSettings.ContainsKey(user.Id)).ToList();
                if (projects.Count > 0) {
                    foreach (Project project in projects)
                        project.NotificationSettings.Remove(user.Id);

                    _projectRepository.Update(projects);
                }

                user.OrganizationIds.Remove(organization.Id);
                _userRepository.Update(user);
            }

            _notificationSender.OrganizationUpdated(organization.Id);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private bool IsNameAvailable(string name) {
            if (String.IsNullOrEmpty(name))
                return false;

            return _repository.Count(
                Query.And(
                     Query.In(OrganizationRepository.FieldNames.Id, User.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id)))),
                     Query.EQ(OrganizationRepository.FieldNames.Name, name))) == 0;
        }

        protected virtual HttpResponseMessage BadRequestErrorResponseMessage(string message = "Invalid Organization Id.") {
            return Request != null
                ? Request.CreateErrorResponse(HttpStatusCode.BadRequest, message)
                : new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        public enum OrganizationSortBy {
            Newest = 0,
            MostActive = 1,
            Alphabetical = 2,
        }
    }
}