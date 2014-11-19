(function () {
  'use strict';

  angular.module('app', [
    'ngAnimate',
    'ui.bootstrap',
    'ui.utils',
    'ui.router',
    'restangular',
    'angular-filters',
    'angular-rickshaw',
    'checklist-model',
    'debounce',

    'exceptionless.date-filter',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.filter',
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
    'app.admin',
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
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['filterService', function (filterService) {
          filterService.setOrganizationId(null);
        }]
      });

      $stateProvider.state('app.project-dashboard', {
        url: '/project/:id/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
        }]
      });

      $stateProvider.state('app.project-type-dashboard', {
        url: '/project/:id/:type/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          console.log('onexit');
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-dashboard', {
        url: '/organization/:id/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.id);
        }]
      });

      $stateProvider.state('app.organization-type-dashboard', {
        url: '/organization/:id/:type/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.type-dashboard', {
        url: '/type/:type/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.frequent', {
        url: '/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['filterService', function (filterService) {
          filterService.setOrganizationId(null);
        }]
      });

      $stateProvider.state('app.project-frequent', {
        url: '/project/:id/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
        }]
      });

      $stateProvider.state('app.project-type-frequent', {
        url: '/project/:id/:type/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-frequent', {
        url: '/organization/:id/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.id);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-frequent', {
        url: '/organization/:id/:type/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.type-frequent', {
        url: '/type/:type/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId(null);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.new', {
        url: '/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['filterService', function (filterService) {
          filterService.setOrganizationId(null);
        }]
      });

      $stateProvider.state('app.project-new', {
        url: '/project/:id/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
        }]
      });

      $stateProvider.state('app.project-type-new', {
        url: '/project/:id/:type/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-new', {
        url: '/organization/:id/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.id);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-new', {
        url: '/organization/:id/:type/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.type-new', {
        url: '/type/:type/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId(null);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.recent', {
        url: '/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['filterService', function (filterService) {
          filterService.setOrganizationId(null);
        }]
      });

      $stateProvider.state('app.project-recent', {
        url: '/project/:id/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
        }]
      });

      $stateProvider.state('app.project-type-recent', {
        url: '/project/:id/:type/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-recent', {
        url: '/organization/:id/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.id);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-recent', {
        url: '/organization/:id/:type/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.id);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.type-recent', {
        url: '/type/:type/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId(null);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });
    }]);
}());
