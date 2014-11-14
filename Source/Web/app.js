(function () {
  'use strict';

  angular.module('app', [
    'ngAnimate',
    'ui.bootstrap',
    'ui.utils',
    'ui.router',
    'restangular',
    'angular-rickshaw',
    'checklist-model',
    'debounce',

    'exceptionless.date-filter',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.notification',
    'exceptionless.project-filter',
    'exceptionless.refresh',
    'exceptionless.search-filter',
    'exceptionless.signalr',
    'exceptionless.stack',
    'exceptionless.stacks',
    'exceptionless.stat',
    'exceptionless.ui-nav',
    'exceptionless.ui-scroll',
    'exceptionless.ui-shift',
    'exceptionless.ui-toggle-class',
    'app.config',
    'app.account',
    'app.event',
    'app.organization',
    'app.project',
    'app.stack'
  ])
  .config(['$stateProvider', '$urlRouterProvider', 'RestangularProvider', 'BASE_URL', function ($stateProvider, $urlRouterProvider, RestangularProvider, BASE_URL) {
    RestangularProvider.setBaseUrl(BASE_URL + '/api/v2');
    RestangularProvider.setDefaultHttpFields({withCredentials: true});
    RestangularProvider.setDefaultRequestParams({access_token: 'd795c4406f6b4bc6ae8d787c65d0274d'});
    RestangularProvider.setFullResponse(true);
    //RestangularProvider.setDefaultHeaders({  'Content-Type': 'application/json' });

    $urlRouterProvider.otherwise('/dashboard');
    $stateProvider.state('app', {
      abstract: true,
      templateUrl: 'app/app.tpl.html'
    });

    $stateProvider.state('app.dashboard', {
      url: '/dashboard',
      controller: 'app.Dashboard',
      controllerAs: 'vm',
      templateUrl: 'app/dashboard.tpl.html'
      /*
       Note: this is how you implement optional parameters.
       params: {
       id: function() {
       // TODO: Resolve current project id from service.
       return '537650f3b77efe23a47914f4';
       }
       }*/
    });

    $stateProvider.state('app.frequent', {
      url: '/frequent',
      controller: 'app.Frequent',
      controllerAs: 'vm',
      templateUrl: 'app/frequent.tpl.html'
    });

    $stateProvider.state('app.new', {
      url: '/new',
      controller: 'app.New',
      controllerAs: 'vm',
      templateUrl: 'app/new.tpl.html'
    });

    $stateProvider.state('app.recent', {
      url: '/recent',
      controller: 'app.Recent',
      controllerAs: 'vm',
      templateUrl: 'app/recent.tpl.html'
    });
  }]);
  /* Required for optional parameters.
   .run(['$urlMatcherFactory', function($urlMatcherFactory){
   // Required for default params.
   }]);
   */
}());
