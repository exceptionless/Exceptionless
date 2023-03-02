(function () {
  'use strict';

  angular.module('app.stack', [
    'cfp.hotkeys',
    'ngMessages',
    'ui.bootstrap',
    'ui.router',

    'dialogs.main',

    'exceptionless',
    'exceptionless.autofocus',
    'exceptionless.dialog',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.filter',
    'exceptionless.log-level',
    'exceptionless.organization',
    'exceptionless.organization-notifications',
    'exceptionless.promise-button',
    'exceptionless.notification',
    'exceptionless.rate-limit',
    'exceptionless.refresh',
    'exceptionless.stack',
    'exceptionless.stack-dialog',
    'exceptionless.stack-trace'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('app.stack', {
      title: 'Stack',
      url: '/stack/{id:[0-9a-fA-F]{24}}',
      controller: 'Stack',
      controllerAs: 'vm',
      templateUrl: 'app/stack/stack.tpl.html'
    });

    $stateProvider.state('app.stack-action', {
      title: 'Stack',
      url: '/stack/{id:[0-9a-fA-F]{24}}/:action',
      controller: 'Stack',
      controllerAs: 'vm',
      templateUrl: 'app/stack/stack.tpl.html'
    });
  });
}());
