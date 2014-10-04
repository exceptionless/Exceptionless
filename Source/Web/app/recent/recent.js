(function () {
    'use strict';

    angular.module('app.recent', [
        'exceptionless.stack',
        'exceptionless.stacks'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.recent', {
            url: '/recent',
            controller: 'Recent',
            controllerAs: 'vm',
            templateUrl: 'app/recent/recent.tpl.html'
        });
    });
}());
