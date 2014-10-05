(function () {
    'use strict';

    angular.module('app.project', [
        'exceptionless.project',
        'exceptionless.projects'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.project', {
            abstract: true,
            url: '/project',
            template: '<ui-view/>'
        });
        $stateProvider.state('app.project.list', {
            url: '/list',
            controller: 'List',
            controllerAs: 'vm',
            templateUrl: 'app/project/list.tpl.html'
        });
    });
}());
