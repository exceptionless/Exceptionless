#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Mvc;
using AutoMapper;
using Exceptionless.App.Models.Error;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Web.Controllers;

namespace Exceptionless.App.Controllers {
    [Authorize]
    [ProjectRequiredActionFilter]
    public class ErrorController : ExceptionlessController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _repository;

        public ErrorController(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, IEventRepository repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _repository = repository;
        }

        [NotSuspended]
        public ActionResult Index(string id) {
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            Error error = _repository.GetByIdCached(id);
            if (error == null || !User.CanAccessOrganization(error.OrganizationId))
                return HttpNotFound("An error with this id was not found.");

            RouteData.SetOrganizationId(error.OrganizationId);

            Project project = _projectRepository.GetByIdCached(error.ProjectId);
            ErrorModel model = Mapper.Map<Error, ErrorModel>(error);
            model.OccurrenceDate = TimeZoneInfo.ConvertTime(error.OccurrenceDate, project.DefaultTimeZone());
            model.PreviousErrorId = _repository.GetPreviousEventIdInStack(error.Id);
            model.NextErrorId = _repository.GetNextEventIdInStack(error.Id);
            model.PromotedTabs = project.PromotedTabs;
            model.CustomContent = project.CustomContent;

            DateTime retentionUtcCutoff = _organizationRepository.GetByIdCached(project.OrganizationId).GetRetentionUtcCutoff();
            if (model.OccurrenceDate.UtcDateTime <= retentionUtcCutoff)
                return View("RetentionLimitReached", model);

            return View("ErrorOccurrence", model);
        }

        public ActionResult Notification(string stackId, string errorId) {
            if (String.IsNullOrEmpty(stackId) || String.IsNullOrEmpty(errorId))
                return RedirectToAction("Index", "Project");

            Error error = _repository.GetByIdCached(errorId);
            if (error != null)
                return RedirectToAction("Index", new { id = errorId });

            Stack stack = _stackRepository.GetByIdCached(stackId);
            if (stack != null && User.CanAccessOrganization(stack.OrganizationId))
                return View("OccurrenceNotFound", stack);

            return HttpNotFound("An error with this id was not found.");
        }
    }
}