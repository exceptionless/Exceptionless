(function () {
  'use strict';

  angular.module('app.account', [
    'directives.inputMatch',
    'ngMessages',
    'ui.bootstrap',
    'ui.router',

    'exceptionless',
    'exceptionless.auth',
    'exceptionless.autofocus',
    'exceptionless.billing',
    'exceptionless.dialog',
    'exceptionless.project',
    'exceptionless.promise-button',
    'exceptionless.user',
    'exceptionless.validators'
  ])
    .config(function ($stateProvider) {
      $stateProvider.state('app.account', {
        abstract: true,
        url: '/account',
        template: '<ui-view autoscroll="true" />'
      });

      $stateProvider.state('app.account.manage', {
        title: 'My Account',
        url: '/manage?projectId&tab',
        controller: 'account.Manage',
        controllerAs: 'vm',
        templateUrl: 'app/account/manage.tpl.html'
      });

      $stateProvider.state('app.account.verify', {
        title: 'Verify Account',
        url: '/verify?token',
        template: null,
        controller: 'account.Verify'
      });
    });
}());
