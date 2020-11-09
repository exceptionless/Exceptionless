(function () {
  'use strict';

  angular.module('app.organization', [
    'angular-rickshaw',
    'ngMessages',
    'ui.bootstrap',
    'ui.router',

    'dialogs.main',

    'app.config',
    'exceptionless',
    'exceptionless.autofocus',
    'exceptionless.billing',
    'exceptionless.dialog',
    'exceptionless.filter',
    'exceptionless.organization',
    'exceptionless.pagination',
    'exceptionless.project',
    'exceptionless.projects',
    'exceptionless.refresh',
    'exceptionless.timeago',
    'exceptionless.token',
    'exceptionless.user',
    'exceptionless.users',
    'exceptionless.validators',
    'exceptionless.web-hook'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('app.organization', {
      abstract: true,
      url: '/organization',
      template: '<ui-view autoscroll="true" />'
    });

    $stateProvider.state('app.organization.list', {
      title: 'My Organizations',
      url: '/list',
      controller: 'organization.List',
      controllerAs: 'vm',
      templateUrl: 'app/organization/list/list.tpl.html'
    });

    $stateProvider.state('app.organization.manage', {
      title: 'Manage Organization',
      url: '/{id:[0-9a-fA-F]{24}}/manage?tab',
      controller: 'organization.Manage',
      controllerAs: 'vm',
      templateUrl: 'app/organization/manage/manage.tpl.html'
    });

    $stateProvider.state('app.organization.upgrade', {
      title: 'Upgrade Organization',
      url: '/{id:[0-9a-fA-F]{24}}/upgrade',
      controller: 'organization.Upgrade',
      template: null
    });
  });
}());
