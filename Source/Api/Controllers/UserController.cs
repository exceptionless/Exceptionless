using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using FluentValidation;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/users")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class UserController : RepositoryApiController<IUserRepository, User, ViewUser, User, UpdateUser> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IMailer _mailer;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository, IMailer mailer) : base(userRepository) {
            _organizationRepository = organizationRepository;
            _mailer = mailer;
        }

        /// <summary>
        /// Get current user
        /// </summary>
        /// <response code="404">The current user could not be found.</response>
        [HttpGet]
        [Route("me")]
        [ResponseType(typeof(ViewCurrentUser))]
        public IHttpActionResult GetCurrentUser() {
            var currentUser = GetModel(ExceptionlessUser.Id);
            if (currentUser == null)
                return NotFound();

            return Ok(new ViewCurrentUser(currentUser));
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <response code="404">The user could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetUserById")]
        [ResponseType(typeof(ViewUser))]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        /// <summary>
        /// Get by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/users")]
        [ResponseType(typeof(List<ViewUser>))]
        public IHttpActionResult GetByOrganization(string organizationId, int page = 1, int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            List<ViewUser> results = _repository.GetByOrganizationId(organizationId).Select(Mapper.Map<User, ViewUser>).ToList();
            var org = _organizationRepository.GetById(organizationId, true);
            if (org.Invites.Any())
                results.AddRange(org.Invites.Select(i => new ViewUser { EmailAddress = i.EmailAddress, IsInvite = true }));

            page = GetPage(page);
            limit = GetLimit(limit);
            return OkWithResourceLinks(results.Skip(GetSkip(page, limit)).Take(limit).ToList(), results.Count > limit, page);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <param name="changes">The changes</param>
        /// <response code="400">An error occurred while updating the user.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpPatch]
        [HttpPut]
        [Route("{id:objectid}")]
        public override IHttpActionResult Patch(string id, Delta<UpdateUser> changes) {
            return base.Patch(id, changes);
        }

        /// <summary>
        /// Update email address
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <param name="email">The new email address.</param>
        /// <response code="400">An error occurred while updating the users email address.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpPost]
        [Route("{id:objectid}/email-address/{email:minlength(1)}")]
        [ResponseType(typeof(UpdateEmailAddressResult))]
        public IHttpActionResult UpdateEmailAddress(string id, string email) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            email = email.ToLower();
            if (!IsEmailAddressAvailableInternal(email))
                return BadRequest("A user with this email address already exists.");

            if (String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });

            user.EmailAddress = email;
            user.IsEmailAddressVerified = user.OAuthAccounts.Count(oa => String.Equals(oa.EmailAddress(), email, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!user.IsEmailAddressVerified)
                user.CreateVerifyEmailAddressToken();

            try {
                _repository.Save(user);
            } catch (ValidationException ex) {
                return BadRequest(String.Join(", ", ex.Errors));
            } catch (Exception ex) {
                Log.Error().Exception(ex).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("An error occurred.");
            }

            if (!user.IsEmailAddressVerified)
                ResendVerificationEmail(id);

            return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });
        }

        /// <summary>
        /// Verify email address
        /// </summary>
        /// <param name="token">The token identifier.</param>
        /// <response code="400">Verify Email Address Token has expired.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpGet]
        [Route("verify-email-address/{token:token}")]
        public IHttpActionResult Verify(string token) {
            var user = _repository.GetByVerifyEmailAddressToken(token);
            if (user == null)
                return NotFound();

            if (!user.HasValidVerifyEmailAddressTokenExpiration())
                return BadRequest("Verify Email Address Token has expired.");

            user.MarkEmailAddressVerified();
            _repository.Save(user);

            //ExceptionlessClient.Default.CreateFeatureUsage("Verify Email Address").AddObject(user).Submit();
            return Ok();
        }

        /// <summary>
        /// Resend verification email
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <response code="404">The user could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}/resend-verification-email")]
        public IHttpActionResult ResendVerificationEmail(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();
            
            if (!user.IsEmailAddressVerified) {
                user.CreateVerifyEmailAddressToken();
                _repository.Save(user);
                _mailer.SendVerifyEmail(user);
            }

            return Ok();
        }

        [HttpPost]
        [Route("{id:objectid}/admin-role")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult AddAdminRole(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);
                _repository.Save(user, true);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/admin-role")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult DeleteAdminRole(string id) {
            var user = GetModel(id, false);
            if (user == null)
                return NotFound();

            if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Remove(AuthorizationRoles.GlobalAdmin);
                _repository.Save(user, true);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        private bool IsEmailAddressAvailableInternal(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return false;

            if (ExceptionlessUser != null && String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return true;

            return _repository.GetByEmailAddress(email) == null;
        }

        protected override User GetModel(string id, bool useCache = true) {
            if (Request.IsGlobalAdmin() || String.Equals(ExceptionlessUser.Id, id))
                return base.GetModel(id, useCache);

            return null;
        }

        protected override ICollection<User> GetModels(string[] ids, bool useCache = true) {
            if (Request.IsGlobalAdmin())
                return base.GetModels(ids, useCache);

            return base.GetModels(ids.Where(id => String.Equals(ExceptionlessUser.Id, id)).ToArray(), useCache);
        }
    }
}