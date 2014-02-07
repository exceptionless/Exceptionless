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
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Mvc;
using AutoMapper;
using DotNetOpenAuth.AspNet;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Utility;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models.Account;
using Exceptionless.Web.Models.Common;
using Exceptionless.Web.Models.Project;
using Exceptionless.Web.Models.User;
using Newtonsoft.Json;

namespace Exceptionless.Web.Controllers {
    [Authorize]
    public class AccountController : ExceptionlessController {
        private readonly IMembershipProvider _membershipProvider;
        private readonly IMembershipSecurity _encoder = new DefaultMembershipSecurity();
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;
        private readonly IMailer _mailer;
        private readonly DataHelper _dataHelper;

        private static bool _isFirstUserChecked;

        public AccountController(IMembershipProvider membership, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, BillingManager billingManager, NotificationSender notificationSender, IMailer mailer, DataHelper dataHelper) {
            _membershipProvider = membership;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _mailer = mailer;
            _dataHelper = dataHelper;
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public ActionResult Init(string projectId = null, string organizationId = null) {
            List<Organization> organizations = _organizationRepository.GetByIds(User.GetAssociatedOrganizationIds()).ToList();
            List<Project> projects = _projectRepository.GetByOrganizationIds(User.GetAssociatedOrganizationIds()).ToList();

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
                    if (organization != null)
                        organizations.Add(organization);
                }
            }

            return Json(new {
                User = new UserModel {
                    Id = User.UserEntity.Id,
                    FullName = User.UserEntity.FullName,
                    EmailAddress = User.UserEntity.EmailAddress,
                    IsEmailAddressVerified = User.UserEntity.IsEmailAddressVerified,
                    HasAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin)
                },
                EnableBilling = Settings.Current.EnableBilling,
                BillingInfo = BillingManager.Plans,
                Organizations = organizations,
                Projects = projects.Select(Mapper.Map<Project, ProjectInfoModel>),
            }, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl) {
            if (User != null)
                return String.IsNullOrEmpty(returnUrl) ? RedirectToAction("Index", "Project") : RedirectToLocal(returnUrl);

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model, string returnUrl) {
            if (!ModelState.IsValid)
                return View(model);

            try {
                if (_membershipProvider.Login(model.EmailAddress, model.Password, model.RememberMe)) {
                    if (String.IsNullOrEmpty(returnUrl))
                        return RedirectToAction("Index", "Project");

                    return RedirectToLocal(returnUrl);
                }
            } catch (InvalidOperationException ex) {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }

            ModelState.AddModelError("", "The email address or password provided is incorrect.");
            return View(model);
        }

        public ActionResult LogOff() {
            _membershipProvider.Logout();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult Signup(string token) {
            if (User != null) // NOTE: We shouldn't ever need to check the token as the user should automatically be added to the project... but it might not hurt to.
                return RedirectToAction("Index", "Project");

            if (!String.IsNullOrEmpty(token)) {
                Invite invite;
                Organization organization = _organizationRepository.GetByInviteToken(token, out invite);
                if (organization != null) {
                    ViewBag.Organization = organization;
                    ViewBag.Token = token;
                    ViewBag.ReturnUrl = Url.Action("Index", "Project");

                    return View(new RegisterModel { EmailAddress = invite.EmailAddress });
                }

                SetErrorAlert("This invite is no longer valid.");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Signup(RegisterModel model, string returnUrl, string token) {
            if (ModelState.IsValid) {
                // Attempt to register the user
                try {
                    User eu = _membershipProvider.GetUserByEmailAddress(model.EmailAddress);
                    if (eu != null) {
                        ModelState.AddModelError("EmailAddress", "A user already exists with this email address.");
                        return View(model);
                    }

                    var user = new User { EmailAddress = model.EmailAddress, Password = model.Password, FullName = model.FullName };
                    user.Roles.Add(AuthorizationRoles.User);

                    // Add the GlobalAdmin role to the first user of the system.
                    if (!_isFirstUserChecked) {
                        _isFirstUserChecked = true;

                        if (_userRepository.All().FirstOrDefault() == null)
                            user.Roles.Add(AuthorizationRoles.GlobalAdmin);
                    }

                    _membershipProvider.CreateAccount(user);

                    if (!String.IsNullOrEmpty(token)) {
                        AddInvitedUserToOrganization(token, user);
                    } else {
                        // TODO: We should verify that the passed in token was valid. If it wasn't, we should resend the verify email token.
                        user.VerifyEmailAddressToken = _membershipProvider.GenerateVerifyEmailToken(user.EmailAddress);
                        _mailer.SendVerifyEmailAsync(user);
                    }

                    if (Settings.Current.WebsiteMode == WebsiteMode.Dev && user.Roles.Contains(AuthorizationRoles.GlobalAdmin)) {
                        _dataHelper.CreateSampleOrganizationAndProject(user.Id);
                        return String.IsNullOrEmpty(returnUrl) ? RedirectToAction("Index", "Project") : RedirectToLocal(returnUrl);
                    }

                    return String.IsNullOrEmpty(returnUrl) ? RedirectToAction("Add", "Project") : RedirectToLocal(returnUrl);
                } catch (MembershipException e) {
                    _isFirstUserChecked = false;
                    ModelState.AddModelError("", ErrorCodeToString(e.StatusCode));
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        private void AddInvitedUserToOrganization(string token, User user) {
            Invite invite;
            Organization organization = _organizationRepository.GetByInviteToken(token, out invite);
            if (organization != null) {
                if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                    user.IsEmailAddressVerified = true;
                    _userRepository.Update(user);
                }

                if (!_billingManager.CanAddUser(organization)) {
                    ModelState.AddModelError(String.Empty, "Please upgrade your plan to add an additional user.");
                    return;
                }

                user.OrganizationIds.Add(organization.Id);
                _userRepository.Update(user);

                organization.Invites.Remove(invite);
                _organizationRepository.Update(organization);
                _notificationSender.OrganizationUpdated(organization.Id);
            }
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public ActionResult Disassociate(string provider, string providerUserId) {
            _membershipProvider.DeleteOAuthAccount(provider, providerUserId);
            return RedirectToAction("Manage", new { Message = "Complete" });
        }

        public ActionResult Manage(ManageMessageId? message) {
            var model = new ManageModel();

            ViewBag.StatusMessage =
                message == ManageMessageId.ChangeProfileSuccess ? "Your profile has been updated."
                    : message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                        : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                            : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                                : "";

            ViewBag.HasLocalPassword = _membershipProvider.HasLocalAccount(User.Identity.Name);
            ViewBag.ReturnUrl = Url.Action("Manage");

            User user = _membershipProvider.GetUserByEmailAddress(User.Identity.Name);
            model.EmailAddress = user.EmailAddress;
            model.EmailNotificationsEnabled = user.EmailNotificationsEnabled;
            model.FullName = user.FullName;
            ViewBag.IsVerified = user.IsEmailAddressVerified;

            return View(model);
        }

        [HttpPost]
        //[ValidateJsonAntiForgeryToken]
        public ActionResult Manage(ManageModel model) {
            ModelState state = ModelState["OldPassword"];
            if (state != null)
                state.Errors.Clear();

            state = ModelState["NewPassword"];
            if (state != null)
                state.Errors.Clear();

            state = ModelState["ConfirmPassword"];
            if (state != null)
                state.Errors.Clear();

            User user = User.UserEntity;
            if (ModelState.IsValid) {
                try {
                    _userRepository.InvalidateCache(user);

                    if (!String.Equals(user.EmailAddress, model.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                        if (_userRepository.GetByEmailAddress(model.EmailAddress) != null)
                            throw new InvalidOperationException("A user with this email address already exists.");

                        user.IsEmailAddressVerified = user.OAuthAccounts.Count(oa => String.Equals(oa.EmailAddress(), model.EmailAddress, StringComparison.OrdinalIgnoreCase)) > 0;
                    }

                    user.EmailAddress = model.EmailAddress;
                    user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;
                    user.FullName = model.FullName;

                    _membershipProvider.UpdateAccount(user);

                    // NOTE: If a user is updating their profile but hasn't verified the email address.. I think we should send them a notification every time..
                    if (!user.IsEmailAddressVerified) {
                        user.VerifyEmailAddressToken = _membershipProvider.GenerateVerifyEmailToken(user.EmailAddress);
                        _mailer.SendVerifyEmailAsync(user);
                    }

                    var principal = new ExceptionlessPrincipal(user);
                    Thread.CurrentPrincipal = principal;
                    if (System.Web.HttpContext.Current != null)
                        System.Web.HttpContext.Current.User = principal;
                } catch (Exception e) {
                    ModelState.AddModelError("", e.Message);
                }
            }

            if (!ModelState.IsValid) {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(ModelState.ToDictionary());
            }

            return Json(new { IsVerified = user.IsEmailAddressVerified });
        }

        [HttpPost]
        //[ValidateJsonAntiForgeryToken]
        public ActionResult ManagePassword(LocalPasswordModel model) {
            bool hasLocalAccount = _membershipProvider.HasLocalAccount(User.Identity.Name);
            if (hasLocalAccount) {
                if (ModelState.IsValid) {
                    // ChangePassword will throw an exception rather than return false in certain failure scenarios.
                    bool changePasswordSucceeded;
                    try {
                        changePasswordSucceeded = _membershipProvider.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword);
                    } catch (Exception) {
                        changePasswordSucceeded = false;
                    }

                    if (!changePasswordSucceeded)
                        ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
                }
            } else {
                // User does not have a local password so remove any validation errors caused by a missing
                // OldPassword field
                ModelState state = ModelState["OldPassword"];
                if (state != null)
                    state.Errors.Clear();

                if (ModelState.IsValid) {
                    try {
                        _membershipProvider.SetLocalPassword(User.Identity.Name, model.NewPassword);
                    } catch (MembershipException e) {
                        ModelState.AddModelError("", e.Message);
                    }
                }
            }

            if (!ModelState.IsValid) {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(ModelState.ToDictionary());
            }

            return Json(true);
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword() {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(EmailAddressModel model) {
            if (ModelState.IsValid) {
                // Attempt to send the user a password email
                try {
                    _membershipProvider.GeneratePasswordResetToken(model.EmailAddress);
                } catch (MembershipException) {
                    ModelState.AddModelError("EmailAddress", "User not found.");
                    return View();
                }

                try {
                    User user = _membershipProvider.GetUserByEmailAddress(model.EmailAddress);
                    _mailer.SendPasswordResetAsync(user);
                } catch (Exception ex) {
                    ModelState.AddModelError("", "Unable to send password reset email.");
                    ex.ToExceptionless().SetUserEmail(model.EmailAddress).Submit();

                    return View();
                }

                ViewBag.EmailSent = true;

                return View();
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string token, bool cancel = false) {
            if (cancel)
                _membershipProvider.CancelPasswordReset(token);

            var model = new ResetPasswordModel { ResetPasswordToken = token, Cancelled = cancel };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordModel model) {
            if (ModelState.IsValid) {
                if (_membershipProvider.ResetPassword(model.ResetPasswordToken, model.NewPassword))
                    return RedirectToAction("Login");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl, string token) {
            return new ExternalLoginResult(_membershipProvider, provider, Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl, Token = token }));
        }

        [AllowAnonymous]
        public ActionResult ExternalLoginCallback(string returnUrl, string token) {
            AuthenticationResult result = _membershipProvider.VerifyOAuthAuthentication(Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl, Token = token }));
            if (!result.IsSuccessful) {
                SetErrorAlert(result.Error != null ? result.Error.Message : "An error occurred while trying to login.");
                return RedirectToAction("ExternalLoginFailure");
            }

            // TODO: Need to check to see if we have a user with the specified email address already.
            OAuthAccount account = result.ToOAuthAccount();
            if (_membershipProvider.OAuthLogin(account, remember: true)) {
                if (!String.IsNullOrEmpty(token))
                    AddInvitedUserToOrganization(token, _membershipProvider.GetUserByEmailAddress(account.EmailAddress() ?? account.Username));

                return RedirectToLocal(returnUrl);
            }

            // If the current user is logged in, then add the new OAuth account.
            if (User != null && User.Identity.IsAuthenticated) {
                var user = _membershipProvider.CreateOAuthAccount(account, new User { EmailAddress = User.Identity.Name }); // TODO: @eric, why not pass in: User.UserEntity....
                if (!String.IsNullOrEmpty(token))
                    AddInvitedUserToOrganization(token, user);

                return RedirectToLocal(returnUrl);
            }

            // User is new, ask for their desired membership name
            string loginData = _encoder.Protect(JsonConvert.SerializeObject(account));
            ViewBag.ProviderDisplayName = _membershipProvider.GetOAuthClientData(result.Provider).DisplayName;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Token = token;

            return View("ExternalLoginConfirmation", new ExternalLoginConfirmationModel {
                FullName = account.FullName(),
                EmailAddress = account.EmailAddress(),
                ExternalLoginData = loginData
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLoginConfirmation(ExternalLoginConfirmationModel model, string returnUrl, string token) {
            string accountData;

            // TODO: @eric, The local account might never be created??
            if ((User != null && User.Identity.IsAuthenticated) || !_encoder.TryUnprotect(model.ExternalLoginData, out accountData))
                return RedirectToAction("Manage");

            var account = JsonConvert.DeserializeObject<OAuthAccount>(accountData);

            if (ModelState.IsValid) {
                if (!_membershipProvider.HasLocalAccount(model.EmailAddress)) {
                    var roles = new Collection<string> { AuthorizationRoles.User };

                    // Add the GlobalAdmin role to the first user of the system.
                    if (!_isFirstUserChecked) {
                        _isFirstUserChecked = true;
                        if (_userRepository.All().FirstOrDefault() == null)
                            roles.Add(AuthorizationRoles.GlobalAdmin);
                    }

                    // TODO: Should we be using and merging User.UserEntity if authenticated?
                    User user = _membershipProvider.CreateOAuthAccount(account, new User {
                        EmailAddress = model.EmailAddress,
                        FullName = model.FullName,
                        Roles = roles
                    }); 

                    if (!String.IsNullOrEmpty(token))
                        AddInvitedUserToOrganization(token, user);

                    _membershipProvider.OAuthLogin(account, remember: true);

                    if (!user.IsEmailAddressVerified) {
                        user.VerifyEmailAddressToken = _membershipProvider.GenerateVerifyEmailToken(user.EmailAddress);
                        _mailer.SendVerifyEmailAsync(user);
                    }

                    if (Settings.Current.WebsiteMode == WebsiteMode.Dev && user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
                        _dataHelper.CreateSampleOrganizationAndProject(user.Id);

                    return RedirectToLocal(returnUrl);
                }

                ModelState.AddModelError("EmailAddress", "Email Address already exists. Please enter a different Email Address.");
            }

            _isFirstUserChecked = false;
            ViewBag.ProviderDisplayName = _membershipProvider.GetOAuthClientData(account.Provider).DisplayName;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Token = token;

            return View(model);
        }

        [AllowAnonymous]
        public ActionResult ExternalLoginFailure() {
            return View();
        }

        [AllowAnonymous]
        [ChildActionOnly]
        public ActionResult ExternalLoginsList(string returnUrl, string token) {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Token = token;
            return PartialView("_ExternalLoginsListPartial", _membershipProvider.RegisteredClientData);
        }

        [ChildActionOnly]
        public ActionResult RemoveExternalLogins() {
            IEnumerable<OAuthAccount> accounts = _membershipProvider.GetOAuthAccountsFromEmailAddress(User.Identity.Name);
            var externalLogins = new List<ExternalLoginModel>();
            foreach (OAuthAccount account in accounts) {
                AuthenticationClientData clientData = _membershipProvider.GetOAuthClientData(account.Provider);
                if (clientData == null)
                    continue;

                externalLogins.Add(new ExternalLoginModel {
                    Provider = account.Provider,
                    ProviderDisplayName = clientData.DisplayName,
                    ProviderUserId = account.ProviderUserId,
                    Username = account.Username
                });
            }

            ViewBag.ShowRemoveButton = externalLogins.Count > 1 || _membershipProvider.HasLocalAccount(User.Identity.Name);
            return PartialView("_RemoveExternalLoginsPartial", externalLogins);
        }

        [HttpGet]
        public ActionResult Verify(string token) {
            if (!_membershipProvider.VerifyEmailAddress(token))
                return RedirectToAction("Manage", "Account");

            User user = _userRepository.GetByVerifyEmailAddressToken(token);
            user.IsEmailAddressVerified = true;
            user.VerifyEmailAddressToken = null;
            user.VerifyEmailAddressTokenExpiration = DateTime.MinValue;
            _userRepository.Update(user);

            return RedirectToAction("Manage", "Account");
        }

        [ActionName("resend-verification-email")]
        public ActionResult ResendVerificationEmail() {
            User user = User.UserEntity;
            if (!user.IsEmailAddressVerified) {
                user.VerifyEmailAddressToken = _membershipProvider.GenerateVerifyEmailToken(user.EmailAddress);
                _mailer.SendVerifyEmailAsync(user);
            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult IsEmailAddressAvaliable(string emailAddress) {
            User user = _membershipProvider.GetUserByEmailAddress(emailAddress);
            if (User != null && User.UserEntity != null && String.Equals(User.UserEntity.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase))
                return Json(true, JsonRequestBehavior.AllowGet);

            return Json(user == null, JsonRequestBehavior.AllowGet);
        }

        #region Helpers

        private ActionResult RedirectToLocal(string returnUrl) {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Project");
        }

        internal class ExternalLoginResult : ActionResult {
            private readonly IOAuthProvider _oAuthProvider;

            public ExternalLoginResult(IOAuthProvider oAuthProvider, string provider, string returnUrl) {
                _oAuthProvider = oAuthProvider;
                Provider = provider;
                ReturnUrl = returnUrl;
            }

            public string Provider { get; private set; }
            public string ReturnUrl { get; private set; }

            public override void ExecuteResult(ControllerContext context) {
                _oAuthProvider.RequestOAuthAuthentication(Provider, ReturnUrl);
            }
        }

        private static string ErrorCodeToString(MembershipStatus createStatus) {
            switch (createStatus) {
                case MembershipStatus.DuplicateUserName:
                    return "User name already exists. Please enter a different user name.";

                case MembershipStatus.DuplicateEmail:
                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }

        #endregion

        public enum ManageMessageId {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            ChangeProfileSuccess
        }
    }
}