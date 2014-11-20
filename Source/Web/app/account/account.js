(function () {
  'use strict';

  angular.module('app.account', [
    'ui.router',

    'exceptionless.dialog',
    'exceptionless.project'
  ])
    .config(['$stateProvider', function ($stateProvider) {
      $stateProvider.state('app.account', {
        abstract: true,
        url: '/account',
        template: '<ui-view autoscroll="true" />'
      });

      $stateProvider.state('app.account.manage', {
        url: '/manage',
        controller: 'account.Manage',
        controllerAs: 'vm',
        templateUrl: 'app/account/manage.tpl.html'
      });
    }]);
}());
