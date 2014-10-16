(function () {
    'use strict';

    angular.module('app', [
        'ngAnimate',
        'ui.bootstrap',
        'ui.utils',
        'ui.router',
        'restangular',
        'exceptionless.signalr',
        'exceptionless.ui-loading-bar',
        'exceptionless.ui-nav',
        'exceptionless.ui-scroll',
        'exceptionless.ui-shift',
        'exceptionless.ui-toggle-class',
        'app.event',
        'app.organization',
        'app.project',
        'app.stack'
    ])
    //.constant('BASE_URL', 'http://localhost:50000')
    .constant('BASE_URL', 'https://new.exceptionless.com')
    .config(function ($stateProvider, $urlRouterProvider, RestangularProvider, BASE_URL) {
        RestangularProvider.setBaseUrl(BASE_URL + '/api/v2');
        RestangularProvider.setDefaultHttpFields({ withCredentials: true });
        RestangularProvider.setDefaultRequestParams({ access_token: 'd795c4406f6b4bc6ae8d787c65d0274d' });
        RestangularProvider.setFullResponse(true);
        //RestangularProvider.setDefaultHeaders({  'Content-Type': 'application/json' });

        $urlRouterProvider.otherwise('/project/dashboard');
        $stateProvider.state('app', {
            abstract: true,
            templateUrl: 'app/app.tpl.html'
        });
    });
}());
