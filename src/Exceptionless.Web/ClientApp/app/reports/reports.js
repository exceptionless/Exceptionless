(function () {
  'use strict';

  angular.module('app.reports', [
    'ngMessages',
    'ui.bootstrap',
    'ui.router',

    'dialogs.main',

    'exceptionless',
    'exceptionless.autofocus',
    'exceptionless.billing',
    'exceptionless.dialog',
    'exceptionless.organization-notifications',
    'exceptionless.notification',
    'exceptionless.refresh',
    'exceptionless.stack',
    'exceptionless.stacks',
    'exceptionless.validators'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('app.reports', {
      abstract: true,
      url: '/reports',
      template: '<ui-view autoscroll="true" />'
    });

    var resetEventStatusOnExit = ['filterService', function (filterService) { filterService.setStatus(null, true); }];
    var routeDefaults = {
      controller: 'reports.status',
      controllerAs: 'vm',
      templateUrl: 'app/reports/status.tpl.html',
      title: 'Status'
    };

    $stateProvider.state('app.reports.organization-status', angular.extend({}, {
      url: '/organization/{organizationId:[0-9a-fA-F]{24}}/status/:status',
      onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
        setRouteFilter(filterService, $stateParams.organizationId, null, $stateParams.status);
      }],
      onExit: resetEventStatusOnExit
    }, routeDefaults));

    $stateProvider.state('app.reports.project-status', angular.extend({}, {
      url: '/project/{projectId:[0-9a-fA-F]{24}}/status/:status',
      onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
        setRouteFilter(filterService, null, $stateParams.projectId, $stateParams.status);
      }],
      onExit: resetEventStatusOnExit
    }, routeDefaults));

    $stateProvider.state('app.reports.status', angular.extend({}, {
      url: '/status/:status',
      onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
        setRouteFilter(filterService, null, null, $stateParams.status);
      }],
      onExit: resetEventStatusOnExit
    }, routeDefaults));

    function setRouteFilter(filterService, organizationId, projectId, status) {
      filterService.setOrganizationId(organizationId, true);
      filterService.setProjectId(projectId, true);
      filterService.setStatus(status, true);
    }
  });
}());
