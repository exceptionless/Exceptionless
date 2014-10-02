(function () {
    'use strict';

    angular.module('app.event', [
        'ui.router',
        'angular-filters',
        'exceptionless.event',
        'exceptionless.notification',
        'exceptionless.timeago'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.event', {
            url: '/event/:id',
            controller: 'Event',
            controllerAs: 'vm',
            templateUrl: 'app/event/event.tpl.html'
        });
    });
}());
