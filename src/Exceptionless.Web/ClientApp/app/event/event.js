(function () {
  'use strict';

  angular.module('app.event', [
    'angular-clipboard',
    'angular-filters',
    'cfp.hotkeys',
    'ui.router',

    'exceptionless',
    'exceptionless.billing',
    'exceptionless.dialog',
    'exceptionless.error',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.filter',
    'exceptionless.organization-notifications',
    'exceptionless.link',
    'exceptionless.notification',
    'exceptionless.object-dump',
    'exceptionless.refresh',
    'exceptionless.simple-error',
    'exceptionless.simple-stack-trace',
    'exceptionless.stack-trace',
    'exceptionless.timeago',
    'exceptionless.url'
  ])
    .config(function ($stateProvider) {
      $stateProvider.state('app.event', {
        title: 'Event',
        url: '/event/{id:[0-9a-fA-F]{24}}?tab',
        controller: 'Event',
        controllerAs: 'vm',
        templateUrl: 'app/event/event.tpl.html'
      });

      $stateProvider.state('app.event-reference', {
        title: 'Event Reference',
        url: '/event/by-ref/{referenceId:.{8,100}}',
        controller: 'Reference',
        controllerAs: 'vm',
        templateUrl: 'app/event/reference.tpl.html'
      });
    });
}());
