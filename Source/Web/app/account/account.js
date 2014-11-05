(function () {
    'use strict';

    angular.module('app.account', [
        'ui.router',

        'exceptionless.dialog',
        'exceptionless.project'
    ])
    .config(function ($stateProvider) {
        $stateProvider.state('app.account', {
            abstract: true,
            url: '/account',
            template: '<ui-view/>'
        });

        $stateProvider.state('app.account.manage', {
            url: '/manage',
            controller: 'account.Manage',
            controllerAs: 'vm',
            templateUrl: 'app/account/manage.tpl.html'
        });
    });
}());
