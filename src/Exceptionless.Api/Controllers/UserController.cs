using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.DateTimeExtensions;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX + "/users")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class UserController : RepositoryApiController<IUserRepository, User, ViewUser, User, UpdateUser> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly ICacheClient _cache;
        private readonly IMailer _mailer;

        public UserController(IUserRepository userRepository, IOrganizationRepository organizationRepository, ITokenRepository tokenRepository, ICacheClient cacheClient, IMailer mailer, IMapper mapper, IQueryValidator validator, ILoggerFactory loggerFactory) : base(userRepository, mapper, validator, loggerFactory) {
            _organizationRepository = organizationRepository;
            _tokenRepository = tokenRepository;
            _cache = new ScopedCacheClient(cacheClient, "User");
            _mailer = mailer;
        }

        /// <summary>
        /// Get current user
        /// </summary>
        /// <response code="404">The current user could not be found.</response>
        [HttpGet("me")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewCurrentUser))]
        public async Task<IActionResult> GetCurrentUserAsync() {
            var currentUser = await GetModelAsync(CurrentUser.Id);
            if (currentUser == null)
                return NotFound();

            return Ok(new ViewCurrentUser(currentUser));
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <response code="404">The user could not be found.</response>
        [HttpGet("{id:objectid}", Name = "GetUserById")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewUser))]
        public Task<IActionResult> GetByIdAsync(string id) {
            return GetByIdImplAsync(id);
        }

        /// <summary>
        /// Get by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/users")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ViewUser>))]
        public async Task<IActionResult> GetByOrganizationAsync(string organizationId, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
            if (organization == null)
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            int skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(Enumerable.Empty<ViewUser>());

            var results = await _repository.GetByOrganizationIdAsync(organizationId, o => o.PageLimit(MAXIMUM_SKIP));
            var users = (await MapCollectionAsync<ViewUser>(results.Documents, true)).ToList();
            if (!Request.IsGlobalAdmin())
                users.ForEach(u => u.Roles.Remove(AuthorizationRoles.GlobalAdmin));

            if (organization.Invites.Any()) {
                users.AddRange(organization.Invites.Select(i => new ViewUser {
                    EmailAddress = i.EmailAddress,
                    IsInvite = true
                }));
            }

            var pagedUsers = users.Skip(skip).Take(limit).ToList();
            return OkWithResourceLinks(pagedUsers, users.Count > GetSkip(page + 1, limit), page);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <param name="changes">The changes</param>
        /// <response code="400">An error occurred while updating the user.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpPatch("{id:objectid}")]
        [HttpPut("{id:objectid}")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewUser))]
        public Task<IActionResult> PatchAsync(string id, [FromBody] Delta<UpdateUser> changes) {
            return PatchImplAsync(id, changes);
        }

        /// <summary>
        /// Delete current user
        /// </summary>
        /// <response code="404">The current user could not be found.</response>
        [HttpDelete("me")]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteCurrentUserAsync() {
            return DeleteImplAsync(new [] { CurrentUser.Id });
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of user identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more users were not found.</response>
        /// <response code="500">An error occurred while deleting one or more users.</response>
        [HttpDelete("{ids:objectids}")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        /// <summary>
        /// Update email address
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <param name="email">The new email address.</param>
        /// <response code="400">An error occurred while updating the users email address.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpPost("{id:objectid}/email-address/{email:minlength(1)}")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(UpdateEmailAddressResult))]
        public async Task<IActionResult> UpdateEmailAddressAsync(string id, string email) {
            var user = await GetModelAsync(id, false);
            if (user == null)
                return NotFound();

            email = email.Trim().ToLowerInvariant();
            if (String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
                return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });

            // Only allow 3 email address updates per hour period by a single user.
            string updateEmailAddressAttemptsCacheKey = $"{CurrentUser.Id}:attempts";
            long attempts = await _cache.IncrementAsync(updateEmailAddressAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));
            if (attempts > 3)
                return BadRequest("Update email address rate limit reached. Please try updating later.");

            if (!await IsEmailAddressAvailableInternalAsync(email))
                return BadRequest("A user with this email address already exists.");

            user.ResetPasswordResetToken();
            user.EmailAddress = email;
            user.IsEmailAddressVerified = user.OAuthAccounts.Count(oa => String.Equals(oa.EmailAddress(), email, StringComparison.InvariantCultureIgnoreCase)) > 0;
            if (!user.IsEmailAddressVerified)
                user.CreateVerifyEmailAddressToken();
            else
                user.ResetVerifyEmailAddressToken();

            try {
                await _repository.SaveAsync(user, o => o.Cache());
            } catch (ValidationException ex) {
                return BadRequest(String.Join(", ", ex.Errors));
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Property("User", user).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, ex.Message);
                return BadRequest("An error occurred.");
            }

            if (!user.IsEmailAddressVerified)
                await ResendVerificationEmailAsync(id);

            // TODO: We may want to send an email to old email addresses as well.

            return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });
        }

        /// <summary>
        /// Verify email address
        /// </summary>
        /// <param name="token">The token identifier.</param>
        /// <response code="400">Verify Email Address Token has expired.</response>
        /// <response code="404">The user could not be found.</response>
        [HttpGet("verify-email-address/{token:token}")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> VerifyAsync(string token) {
            var user = await _repository.GetByVerifyEmailAddressTokenAsync(token);
            if (user == null) {
                // The user may already be logged in and verified.
                if (CurrentUser != null && CurrentUser.IsEmailAddressVerified)
                    return Ok();

                return NotFound();
            }

            if (!user.HasValidVerifyEmailAddressTokenExpiration())
                return BadRequest("Verify Email Address Token has expired.");

            user.MarkEmailAddressVerified();
            await _repository.SaveAsync(user, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Resend verification email
        /// </summary>
        /// <param name="id">The identifier of the user.</param>
        /// <response code="404">The user could not be found.</response>
        [HttpGet("{id:objectid}/resend-verification-email")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> ResendVerificationEmailAsync(string id) {
            var user = await GetModelAsync(id, false);
            if (user == null)
                return NotFound();

            if (!user.IsEmailAddressVerified) {
                user.CreateVerifyEmailAddressToken();
                await _repository.SaveAsync(user, o => o.Cache());
                await _mailer.SendUserEmailVerifyAsync(user);
            }

            return Ok();
        }

        [HttpPost("{id:objectid}/admin-role")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> AddAdminRoleAsync(string id) {
            var user = await GetModelAsync(id, false);
            if (user == null)
                return NotFound();

            if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);
                await _repository.SaveAsync(user, o => o.Cache());
            }

            return Ok();
        }

        [HttpDelete("{id:objectid}/admin-role")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> DeleteAdminRoleAsync(string id) {
            var user = await GetModelAsync(id, false);
            if (user == null)
                return NotFound();

            if (user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                user.Roles.Remove(AuthorizationRoles.GlobalAdmin);
                await _repository.SaveAsync(user, o => o.Cache());
            }

            return StatusCode(StatusCodes.Status204NoContent);
        }

        private async Task<bool> IsEmailAddressAvailableInternalAsync(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim().ToLowerInvariant();
            if (CurrentUser != null && String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return await _repository.GetByEmailAddressAsync(email) == null;
        }

        protected override async Task<User> GetModelAsync(string id, bool useCache = true) {
            if (Request.IsGlobalAdmin() || String.Equals(CurrentUser.Id, id))
                return await base.GetModelAsync(id, useCache);

            return null;
        }

        protected override Task<IReadOnlyCollection<User>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (Request.IsGlobalAdmin())
                return base.GetModelsAsync(ids, useCache);

            return base.GetModelsAsync(ids.Where(id => String.Equals(CurrentUser.Id, id)).ToArray(), useCache);
        }

        protected override async Task<PermissionResult> CanDeleteAsync(User value) {
            if (value.OrganizationIds.Count > 0)
                return PermissionResult.DenyWithMessage("Please delete or leave any organizations before deleting your account.");

            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && value.Id != CurrentUser.Id)
                return PermissionResult.Deny;

            return await base.CanDeleteAsync(value);
        }

        protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<User> values) {
            foreach (var user in values) {
                long removed = await _tokenRepository.RemoveAllByUserIdAsync(user.Id);
                _logger.LogInformation("Removed {RemovedCount} tokens for user: {UserId}", removed, user.Id);
            }

            return await base.DeleteModelsAsync(values);
        }
    }
}
