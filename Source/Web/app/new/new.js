(function () {
    'use strict';

    angular.module('app.new', [
        'exceptionless.stack',
        'exceptionless.stacks'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.new', {
            url: '/new',
            controller: 'New',
            controllerAs: 'vm',
            templateUrl: 'app/new/new.tpl.html'
        });
    });
}());
