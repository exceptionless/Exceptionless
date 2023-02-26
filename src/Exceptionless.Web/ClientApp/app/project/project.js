(function () {
  'use strict';

  angular.module('app.project', [
    'angular-clipboard',
    'ngMessages',
    'ui.bootstrap',
    'ui.router',

    'dialogs.main',
    'xeditable',

    'exceptionless',
    'exceptionless.autofocus',
    'exceptionless.billing',
    'exceptionless.dialog',
    'exceptionless.log-level',
    'exceptionless.organization-notifications',
    'exceptionless.project',
    'exceptionless.organization',
    'exceptionless.notification',
    'exceptionless.refresh',
    'exceptionless.token',
    'exceptionless.validators'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('app.project', {
      abstract: true,
      url: '/project',
      template: '<ui-view autoscroll="true" />'
    });

    $stateProvider.state('app.project.add', {
      title: 'Add Project',
      url: '/add?{organizationId:[0-9a-fA-F]{24}}',
      controller: 'project.Add',
      controllerAs: 'vm',
      templateUrl: 'app/project/add.tpl.html'
    });

    $stateProvider.state('app.project.configure', {
      title: 'Configure Project',
      url: '/:id/configure?redirect',
      controller: 'project.Configure',
      controllerAs: 'vm',
      templateUrl: 'app/project/configure.tpl.html'
    });

    $stateProvider.state('app.project.list', {
      title: 'My Projects',
      url: '/list',
      controller: 'project.List',
      controllerAs: 'vm',
      templateUrl: 'app/project/list.tpl.html'
    });

    $stateProvider.state('app.project.manage', {
      title: 'Manage Project',
      url: '/{id:[0-9a-fA-F]{24}}/manage',
      controller: 'project.Manage',
      controllerAs: 'vm',
      templateUrl: 'app/project/manage/manage.tpl.html'
    });
  });
}());
