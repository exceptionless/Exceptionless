using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Models.Project;
using Exceptionless.Api.Models.User;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Membership;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "account")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class AccountController : ExceptionlessApiController {
        private readonly IMembershipProvider _membershipProvider;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IMailer _mailer;

        public AccountController(IMembershipProvider membership, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IMailer mailer) {
            _membershipProvider = membership;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _mailer = mailer;
        }

        [HttpGet]
        [Route("init")]
        public IHttpActionResult Init(string projectId = null, string organizationId = null) {
            List<Organization> organizations = _organizationRepository.GetByIds(Request.GetAssociatedOrganizationIds()).ToList();
            List<Project> projects = _projectRepository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).ToList();

            if (User.IsInRole(AuthorizationRoles.GlobalAdmin)) {
                if (!String.IsNullOrWhiteSpace(projectId) && !projects.Any(p => String.Equals(p.Id, projectId))) {
                    Project project = _projectRepository.GetById(projectId);
                    if (project != null) {
                        projects.Add(project);

                        if (!organizations.Any(o => String.Equals(o.Id, project.OrganizationId)))
                            organizations.Add(_organizationRepository.GetById(project.OrganizationId));
                    }
                }

                if (!String.IsNullOrWhiteSpace(organizationId) && !organizations.Any(o => String.Equals(o.Id, organizationId))) {
                    Organization organization = _organizationRepository.GetById(organizationId);
                    if (organization != null) {
                        organizations.Add(organization);

                        if (!projects.Any(p => String.Equals(p.OrganizationId, organizationId)))
                            projects.AddRange(_projectRepository.GetByOrganizationId(organizationId));
                    }
                }
            }

            var user = Request.GetUser();
            return Ok(new {
                User = new UserModel {
                    Id = user.Id,
                    FullName = user.FullName,
                    EmailAddress = user.EmailAddress,
                    IsEmailAddressVerified = user.IsEmailAddressVerified,
                    HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin)
                },
                EnableBilling = Settings.Current.EnableBilling,
                BillingInfo = BillingManager.Plans,
                Organizations = organizations,
                Projects = projects.Select(Mapper.Map<Project, ProjectInfoModel>),
            });
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("is-email-address-available")]
        public IHttpActionResult IsEmailAddressAvailable(string emailAddress) {
            var currentUser = Request.GetUser();
            if (currentUser != null && String.Equals(currentUser.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase))
                return Ok(true);

            return Ok(_membershipProvider.GetUserByEmailAddress(emailAddress) == null);
        }

        [HttpGet]
        [Route("resend-verification-email")]
        public IHttpActionResult ResendVerificationEmail() {
            User user = Request.GetUser();
            if (!user.IsEmailAddressVerified) {
                user.VerifyEmailAddressToken = _membershipProvider.GenerateVerifyEmailToken(user.EmailAddress);
                _mailer.SendVerifyEmailAsync(user);
            }

            return Ok(true);
        }
    }
}