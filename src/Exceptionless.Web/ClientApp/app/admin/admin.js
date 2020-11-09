(function () {
  'use strict';

  angular.module('app.admin', [
    'ui.router'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('app.admin', {
      abstract: true,
      url: '/admin',
      template: '<ui-view autoscroll="true"/>'
    });

    $stateProvider.state('app.admin.dashboard', {
      title: 'Admin Dashboard',
      url: '/dashboard',
      controller: 'admin.Dashboard',
      controllerAs: 'vm',
      templateUrl: 'app/admin/dashboard.tpl.html'
    });
  });
}());
