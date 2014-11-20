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
        url: '/project/:projectId/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
        }]
      });

      $stateProvider.state('app.project-type-dashboard', {
        url: '/project/:projectId/:type/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          console.log('onexit');
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-dashboard', {
        url: '/organization/:organizationId/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId);
        }]
      });

      $stateProvider.state('app.organization-type-dashboard', {
        url: '/organization/:organizationId/:type/dashboard',
        controller: 'app.Dashboard',
        controllerAs: 'vm',
        templateUrl: 'app/dashboard.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId);
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
        url: '/project/:projectId/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
        }]
      });

      $stateProvider.state('app.project-type-frequent', {
        url: '/project/:projectId/:type/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-frequent', {
        url: '/organization/:organizationId/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.organizationId);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-frequent', {
        url: '/organization/:organizationId/:type/frequent',
        controller: 'app.Frequent',
        controllerAs: 'vm',
        templateUrl: 'app/frequent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId);
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
        url: '/project/:projectId/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
        }]
      });

      $stateProvider.state('app.project-type-new', {
        url: '/project/:projectId/:type/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-new', {
        url: '/organization/:organizationId/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.organizationId);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-new', {
        url: '/organization/:organizationId/:type/new',
        controller: 'app.New',
        controllerAs: 'vm',
        templateUrl: 'app/new.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId);
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
        url: '/project/:projectId/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
        }]
      });

      $stateProvider.state('app.project-type-recent', {
        url: '/project/:projectId/:type/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setProjectId($stateParams.projectId);
          filterService.setEventType($stateParams.type);
        }],
        onExit: ['filterService', function (filterService) {
          filterService.setEventType(null);
        }]
      });

      $stateProvider.state('app.organization-recent', {
        url: '/organization/:organizationId/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        resolve: {
          id: ['$stateParams', 'filterService', function ($stateParams, filterService) {
            filterService.setOrganizationId($stateParams.organizationId);
            return $stateParams.id;
          }]
        }
      });

      $stateProvider.state('app.organization-type-recent', {
        url: '/organization/:organizationId/:type/recent',
        controller: 'app.Recent',
        controllerAs: 'vm',
        templateUrl: 'app/recent.tpl.html',
        onEnter: ['$stateParams', 'filterService', function ($stateParams, filterService) {
          filterService.setOrganizationId($stateParams.organizationId);
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
