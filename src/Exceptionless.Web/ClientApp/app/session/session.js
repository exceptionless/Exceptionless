(function () {
  'use strict';

  angular.module('app.session', [
      'angular-filters',
      'angular-rickshaw',
      'ui.router',

      'exceptionless',
      'exceptionless.duration',
      'exceptionless.events',
      'exceptionless.filter',
      'exceptionless.link',
      'exceptionless.notification',
      'exceptionless.pagination',
      'exceptionless.refresh',
      'exceptionless.organization',
      'exceptionless.organization-notifications',
      'exceptionless.project',
      'exceptionless.summary',
      'exceptionless.timeago',
      'exceptionless.users'
    ])
    .config(function ($stateProvider) {
      var onEnterSetTypeFilter = ['filterService', function (filterService) {
        filterService.setOrganizationId(null, true);
        filterService.setProjectId(null, true);
        filterService.setEventType('session', true);
      }];

      var onExitRemoveTypeFilter = ['filterService', function (filterService) {
        filterService.setEventType(null, true);
      }];

      var title = 'Session Events';
      $stateProvider.state('app.session', {
        abstract: true,
        url: '/session',
        template: '<ui-view autoscroll="true" />'
      });

      $stateProvider.state('app.session.events', {
        title: title,
        url: '/events',
        controller: 'session.Events',
        controllerAs: 'vm',
        templateUrl: 'app/session/events.tpl.html',
        onEnter: onEnterSetTypeFilter,
        onExit: onExitRemoveTypeFilter
      });

      $stateProvider.state('app.session-events', {
        title: title,
        url: '/session/events',
        controller: 'session.Events',
        controllerAs: 'vm',
        templateUrl: 'app/session/events.tpl.html',
        onEnter: onEnterSetTypeFilter,
        onExit: onExitRemoveTypeFilter
      });

      $stateProvider.state('app.session-project-events', {
        title: title,
        url: '/project/{projectId:[0-9a-fA-F]{24}}/session/events',
        controller: 'session.Events',
        controllerAs: 'vm',
        templateUrl: 'app/session/events.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId, true);
          filterService.setEventType('session', true);
        }],
        onExit: onExitRemoveTypeFilter,
        resolve: {
          project: ['$stateParams', 'projectService', function($stateParams, projectService) {
            return projectService.getById($stateParams.projectId, true);
          }]
        }
      });

      $stateProvider.state('app.session-organization-events', {
        title: title,
        url: '/organization/{organizationId:[0-9a-fA-F]{24}}/session/events',
        controller: 'session.Events',
        controllerAs: 'vm',
        templateUrl: 'app/session/events.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId, true);
          filterService.setEventType('session', true);
        }],
        onExit: onExitRemoveTypeFilter,
        resolve: {
          project: ['$stateParams', 'organizationService', function($stateParams, organizationService) {
            return organizationService.getById($stateParams.organizationId, true);
          }]
        }
      });
    });
}());
