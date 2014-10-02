(function () {
    'use strict';

    angular.module('app.dashboard', [
        'exceptionless.event',
        'exceptionless.events',
        'exceptionless.stack',
        'exceptionless.stacks'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.dashboard', {
            url: '/dashboard',
            controller: 'Dashboard',
            controllerAs: 'vm',
            templateUrl: 'app/dashboard/dashboard.tpl.html'
        });
    });
}());