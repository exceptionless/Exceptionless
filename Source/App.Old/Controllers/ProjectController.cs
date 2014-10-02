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
using System.Web.Mvc;
using AutoMapper;
using Exceptionless.App.Models.Project;
using Exceptionless.App.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.App.Controllers {
    [Authorize]
    public class ProjectController : ExceptionlessController {
        private List<Project> _projects;
        private List<Organization> _organizations;

        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;

        public ProjectController(IUserRepository userRepository, IProjectRepository projectRepository, IOrganizationRepository organizationRepository, BillingManager billingManager) {
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        public ActionResult List() {
            return View();
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        [NotSuspended]
        public ActionResult Index(string id) {
            RouteData.SetProjectId(id);
            return View();
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        [NotSuspended]
        public ActionResult New(string id) {
            if (!String.IsNullOrEmpty(id) && _projectRepository.GetById(id, true) == null)
                return RedirectToAction("Index");

            RouteData.SetProjectId(id);
            return View();
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        [NotSuspended]
        public ActionResult Recent(string id) {
            if (!String.IsNullOrEmpty(id) && _projectRepository.GetById(id, true) == null)
                return RedirectToAction("Index");

            RouteData.SetProjectId(id);
            return View();
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        [NotSuspended]
        public ActionResult Frequent(string id) {
            if (!String.IsNullOrEmpty(id) && _projectRepository.GetById(id, true) == null)
                return RedirectToAction("Index");

            RouteData.SetProjectId(id);
            return View();
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        public ActionResult Manage(string id) {
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index");

            Project project = _projectRepository.GetById(id, true);
            if (project == null)
                return RedirectToAction("Index");

            return View(PopulateProjectModel(project));
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        public ActionResult SendSummary(string id) {
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index");

            Project project = _projectRepository.GetById(id);
            if (project == null)
                return RedirectToAction("Index");

            project.NextSummaryEndOfDayTicks = DateTime.UtcNow.AddHours(-9).Ticks;
            _projectRepository.Save(project);

            return RedirectToAction("Index");
        }

        [HttpGet]
        [ProjectRequiredActionFilter]
        public ActionResult Configure(string id) {
            var user = GetUser();
            if (String.IsNullOrEmpty(id) && user != null && user.OrganizationIds.Count > 0) {
                var project = _projectRepository.GetByOrganizationId(user.OrganizationIds.First()).FirstOrDefault();
                if (project != null)
                    id = project.Id;
            }

            if (String.IsNullOrEmpty(id) || _projectRepository.GetById(id, true) == null)
                return RedirectToAction("Index");

            return View();
        }

        [HttpGet]
        public ActionResult Add() {
            ViewBag.Organizations = Organizations;
            ViewBag.HasOrganizations = Organizations.Any();
            ViewBag.TimeZones = TimeZoneInfo.GetSystemTimeZones();

            return View(new ProjectModel());
        }

        // TODO: Move the add project to use the Web API controller via JavaScript.
        [HttpPost]
        public ActionResult Add(ProjectModel model) {
            ViewBag.Organizations = Organizations;
            ViewBag.HasOrganizations = Organizations.Any();
            ViewBag.TimeZones = TimeZoneInfo.GetSystemTimeZones();

            #region Validation

            Organization organization = null;

            if (!String.IsNullOrEmpty(model.OrganizationName)) {
                Organization existing = Organizations.FirstOrDefault(o => o.Name == model.OrganizationName);
                organization = existing ?? new Organization { Name = model.OrganizationName.Trim() };
            } else if (!String.IsNullOrEmpty(model.OrganizationId)) {
                organization = Organizations.FirstOrDefault(o => o.Id == model.OrganizationId);
                ModelState state = ModelState["OrganizationName"];
                if (state != null)
                    state.Errors.Clear();
            }

            if (!ModelState.IsValid)
                return View(model);

            if (organization == null) {
                ModelState.AddModelError("OrganizationName", "Organization Name is required.");
                return View(model);
            }

            if (!String.IsNullOrEmpty(organization.Id) && Projects.Count(p => p.OrganizationId == organization.Id && String.Equals(p.Name, model.Name, StringComparison.OrdinalIgnoreCase)) > 0) {
                ModelState.AddModelError("Name", "A project with this name already exists.");
                return View(model);
            }

            #endregion

            var user = GetUser();
            if (String.IsNullOrEmpty(organization.Id)) {
                if (!_billingManager.CanAddOrganization(user)) {
                    ModelState.AddModelError(String.Empty, "Please upgrade your plan to add an additional organization.");
                    return View(model);
                }

                _billingManager.ApplyBillingPlan(organization, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, user);
                organization = _organizationRepository.Add(organization, true);

                user.OrganizationIds.Add(organization.Id);
                _userRepository.Save(user);
            }

            var project = new Project { Name = model.Name, TimeZone = model.TimeZone, OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), project.DefaultTimeZone()).ToUniversalTime().Ticks;
            project.ApiKeys.Add(Guid.NewGuid().ToString("N").ToLower());
            project.AddDefaultOwnerNotificationSettings(user.Id);

            if (!_billingManager.CanAddProject(project)) {
                ModelState.AddModelError(String.Empty, "Please upgrade your plan to add an additional project.");
                return View(model);
            }

            project = _projectRepository.Add(project);
            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            return RedirectToAction("Configure", "Project", new { id = project.Id });
        }

        private ProjectModel PopulateProjectModel(Project project) {
            var user = GetUser();
            ProjectModel p = Mapper.Map<Project, ProjectModel>(project);
            NotificationSettings notificationSettings = project.GetNotificationSettings(user.Id) ?? new NotificationSettings();
            p.Mode = notificationSettings.Mode;
            p.SendDailySummary = notificationSettings.SendDailySummary;
            p.ReportCriticalErrors = notificationSettings.ReportCriticalErrors;
            p.ReportRegressions = notificationSettings.ReportRegressions;
            p.Report404Errors = notificationSettings.Report404Errors;
            p.ReportKnownBotErrors = notificationSettings.ReportKnownBotErrors;
            p.OrganizationName = _organizationRepository.GetById(project.OrganizationId, true).Name;
            p.UserId = user.Id;

            return p;
        }

        private List<Project> Projects {
            get {
                if (User == null)
                    return new List<Project>();

                if (_projects == null)
                    _projects = _projectRepository.GetByOrganizationIds(GetAssociatedOrganizationIds()).ToList();

                return _projects;
            }
        }

        private List<Organization> Organizations {
            get {
                if (User == null)
                    return new List<Organization>();

                if (_organizations == null)
                    _organizations = _organizationRepository.GetByIds(GetAssociatedOrganizationIds()).ToList();

                return _organizations;
            }
        }
    }
}