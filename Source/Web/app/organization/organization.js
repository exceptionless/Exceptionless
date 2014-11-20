(function () {
  'use strict';

  angular.module('app.organization', [
    'exceptionless.dialog',
    'exceptionless.organization',
    'exceptionless.project',
    'exceptionless.projects',
    'exceptionless.refresh',
    'exceptionless.token',
    'exceptionless.user',
    'exceptionless.web-hook',

    'ui.router',

    // Custom dialog dependencies
    'ui.bootstrap',
    'dialogs.main',
    'dialogs.default-translations'
  ])
    .config(['$stateProvider', function ($stateProvider) {
      $stateProvider.state('app.organization', {
        abstract: true,
        url: '/organization',
        template: '<ui-view autoscroll="true" />'
      });

      $stateProvider.state('app.organization.list', {
        url: '/list',
        controller: 'organization.List',
        controllerAs: 'vm',
        templateUrl: 'app/organization/list/list.tpl.html'
      });

      $stateProvider.state('app.organization.manage', {
        url: '/:id/manage',
        controller: 'organization.Manage',
        controllerAs: 'vm',
        templateUrl: 'app/organization/manage/manage.tpl.html'
      });
    }]);
}());
