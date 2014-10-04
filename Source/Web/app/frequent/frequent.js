(function () {
    'use strict';

    angular.module('app.frequent', [
        'exceptionless.stack',
        'exceptionless.stacks'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.frequent', {
            url: '/frequent',
            controller: 'Frequent',
            controllerAs: 'vm',
            templateUrl: 'app/frequent/frequent.tpl.html'
        });
    });
}());
