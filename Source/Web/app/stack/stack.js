(function () {
  'use strict';

  angular.module('app.stack', [
    'ui.router',
    'exceptionless.dialog',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.feature',
    'exceptionless.filter',
    'exceptionless.notification',
    'exceptionless.refresh',
    'exceptionless.stack',
    'exceptionless.stack-trace',
    'exceptionless.stat',

    // Custom dialog dependencies
    'ui.bootstrap',
    'dialogs.main',
    'dialogs.default-translations'
  ])
    .config(['$stateProvider', function ($stateProvider) {
      $stateProvider.state('app.stack', {
        url: '/stack/:id',
        controller: 'Stack',
        controllerAs: 'vm',
        templateUrl: 'app/stack/stack.tpl.html'
      });
    }]);
}());
