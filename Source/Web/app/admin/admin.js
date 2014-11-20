(function () {
  'use strict';

  angular.module('app.admin', [
    'ui.router'
  ])
  .config(['$stateProvider', function ($stateProvider) {
    $stateProvider.state('app.admin', {
      abstract: true,
      url: '/admin',
      template: '<ui-view autoscroll="true"/>'
    });

    $stateProvider.state('app.admin.dashboard', {
      url: '/dashboard',
      controller: 'admin.Dashboard',
      controllerAs: 'vm',
      templateUrl: 'app/admin/dashboard.tpl.html'
    });
  }]);
}());
