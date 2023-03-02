(function () {
  'use strict';

  angular.module('app', [
    'angular-filters',
    'angular-loading-bar',
    'angular-locker',
    'angular-intercom',
    'angular-rickshaw',
    'angular-stripe',
    'cfp.hotkeys',
    'checklist-model',
    'debounce',
    'ngAnimate',
    'restangular',
    'satellizer',
    'ui.bootstrap',
    'ui.gravatar',
    'ui.router',
    'xeditable',

    'dialogs.main',

    'exceptionless',
    'exceptionless.translate',
    'exceptionless.analytics',
    'exceptionless.auth',
    'exceptionless.auto-active',
    'exceptionless.billing',
    'exceptionless.date-filter',
    'exceptionless.event',
    'exceptionless.events',
    'exceptionless.filter',
    'exceptionless.intercom',
    'exceptionless.loading-bar',
    'exceptionless.notification',
    'exceptionless.organization',
    'exceptionless.organization-notifications',
    'exceptionless.project',
    'exceptionless.project-filter',
    'exceptionless.rate-limit',
    'exceptionless.refresh',
    'exceptionless.release-notification',
    'exceptionless.search-filter',
    'exceptionless.stack',
    'exceptionless.stacks',
    'exceptionless.state',
    'exceptionless.system-notification',
    'exceptionless.translate',
    'exceptionless.ui-nav',
    'exceptionless.ui-scroll',
    'exceptionless.ui-shift',
    'exceptionless.ui-toggle-class',
    'exceptionless.user',
    'exceptionless.websocket',
    'app.account',
    'app.admin',
    'app.auth',
    'app.config',
    'app.event',
    'app.organization',
    'app.payment',
    'app.project',
    'app.reports',
    'app.session',
    'app.stack',
    'app.status'
  ])
    .config(function ($compileProvider, $httpProvider, $locationProvider, $stateProvider, $uiViewScrollProvider, $urlRouterProvider, $translateProvider, dialogsProvider, gravatarServiceProvider, RestangularProvider, BASE_URL, EXCEPTIONLESS_API_KEY, EXCEPTIONLESS_SERVER_URL,$ExceptionlessClient, stripeProvider, STRIPE_PUBLISHABLE_KEY, USE_HTML5_MODE) {
    var lang = (navigator.language ? navigator.language : navigator.browserLanguage || '').toLocaleLowerCase();
    if (lang !== 'zh-cn') {
      lang = 'en-us';
    }

    moment.locale(lang);
    $translateProvider.useSanitizeValueStrategy('escapeParameters');
    $translateProvider.useStaticFilesLoader({
      prefix: '/lang/',
      suffix: '.json'
    });
    $translateProvider.preferredLanguage(lang).fallbackLanguage('en-us');

    function setRouteFilter(filterService, organizationId, projectId, type) {
      filterService.setOrganizationId(organizationId, true);
      filterService.setProjectId(projectId, true);
      filterService.setEventType(type, true);
    }

    $compileProvider.debugInfoEnabled(false);
    $httpProvider.useApplyAsync(true);

    if (EXCEPTIONLESS_API_KEY) {
      var config = $ExceptionlessClient.config;
      config.apiKey = EXCEPTIONLESS_API_KEY;
      config.serverUrl = EXCEPTIONLESS_SERVER_URL || BASE_URL;
      config.defaultTags.push('UI');
      config.setVersion('@@version');
      config.useReferenceIds();
      config.useSessions();
    }

    $locationProvider.html5Mode({
      enabled: (typeof USE_HTML5_MODE === 'boolean' && USE_HTML5_MODE) || USE_HTML5_MODE === 'true',
      requireBase: false
    });

    $uiViewScrollProvider.useAnchorScroll();

    dialogsProvider.setSize('md');

    gravatarServiceProvider.defaults = {
      'default': 'mm'
    };

    RestangularProvider.setBaseUrl(BASE_URL + '/api/v2');
    RestangularProvider.setFullResponse(true);
    RestangularProvider.setDefaultHttpFields({ timeout: 10 * 1000 });

    if (!!STRIPE_PUBLISHABLE_KEY) {
      stripeProvider.setPublishableKey(STRIPE_PUBLISHABLE_KEY);
    }

    $stateProvider.state('app', {
      abstract: true,
      templateUrl: 'app/app.tpl.html',
      controller: 'App',
      controllerAs: 'appVm',
      data: {
        requireAuthentication: true
      },
      resolve: {
        translateReady: function($translate) {
          return $translate.onReady();
        }
      }
    });

    var routes = [
      { key: 'frequent', title: 'Most Frequent', controller: 'app.Frequent' },
      { key: 'recent', title: 'Most Frequent', controller: 'app.Frequent', templateUrl: 'app/frequent.tpl.html', redirectToKey: 'frequent'  },
      { key: 'new', title: 'New Stacks', controller: 'app.New' },
      { key: 'users', title: 'Most Users', controller: 'app.Users' },
      { key: 'events', title: 'Events', controller: 'app.Events' },
      { key: 'timeline', title: 'Events', controller: 'app.Events', templateUrl: 'app/events.tpl.html', redirectToKey: 'events' },
      { key: 'dashboard', title: 'Events', controller: 'app.Events', templateUrl: 'app/events.tpl.html', redirectToKey: 'events' }
    ];
    var resetEventTypeOnExit = ['filterService', function (filterService) { filterService.setEventType(null, true); }];
    routes.forEach(function(route) {
      var routeDefaults = {
        controller: route.controller,
        controllerAs: 'vm',
        templateUrl: route.templateUrl || 'app/' + route.key + '.tpl.html',
        title: route.title
      };

      $stateProvider.state('app.' + route.key, angular.extend({}, {
        url: '/' + route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, null, null, null);
        }]
      }, routeDefaults));

      $stateProvider.state('app.project-' + route.key, angular.extend({}, {
        url: '/project/{projectId:[0-9a-fA-F]{24}}/' + route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.project-' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, null, $stateParams.projectId, null);
        }]
      }, routeDefaults));

      $stateProvider.state('app.project-type-'+ route.key, angular.extend({}, {
        url: '/project/{projectId:[0-9a-fA-F]{24}}/:type/'+ route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if ($stateParams.type === 'session') {
            return $timeout(function () {
              $state.transitionTo('app.session-project-events', $stateParams);
            });
          }

          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.project-type-' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, null, $stateParams.projectId, $stateParams.type);
        }],
        onExit: resetEventTypeOnExit
      }, routeDefaults));

      $stateProvider.state('app.organization-' + route.key, angular.extend({}, {
        url: '/organization/{organizationId:[0-9a-fA-F]{24}}/' + route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.organization-' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, $stateParams.organizationId, null, null);
        }]
      }, routeDefaults));

      $stateProvider.state('app.organization-type-' + route.key, angular.extend({}, {
        url: '/organization/{organizationId:[0-9a-fA-F]{24}}/:type/' + route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if ($stateParams.type === 'session') {
            return $timeout(function () {
              $state.transitionTo('app.session-organization-events', $stateParams);
            });
          }

          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.organization-type-' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, $stateParams.organizationId, null, $stateParams.type);
        }],
        onExit: resetEventTypeOnExit
      }, routeDefaults));

      $stateProvider.state('app.type-' + route.key, angular.extend({}, {
        url: '/type/:type/' + route.key,
        onEnter: ['$state', '$stateParams', '$timeout', 'filterService', function ($state, $stateParams, $timeout, filterService) {
          if ($stateParams.type === 'session') {
            return $timeout(function () {
              $state.transitionTo('app.session-events', $stateParams);
            });
          }

          if (route.redirectToKey) {
            return $timeout(function () {
              $state.transitionTo('app.type-' + route.redirectToKey, $stateParams);
            });
          }

          setRouteFilter(filterService, null, null, $stateParams.type);
        }],
        onExit: resetEventTypeOnExit
      }, routeDefaults));
    });

    var onEnter = ['authService', '$location', '$state', '$timeout', function (authService, $location, $state, $timeout) {
      if ($location.search().code){
        return;
      }

      return $timeout(function () {
        if (authService.isAuthenticated()) {
          $state.transitionTo('app.type-frequent', {type: 'error'});
        } else {
          $state.transitionTo('auth.login');
        }
      });
    }];

    $stateProvider.state('loading', {
      url: '',
      template: null,
      onEnter: onEnter
    });

    $stateProvider.state('loading-slash', {
      url: '/',
      template: null,
      onEnter: onEnter
    });

    $stateProvider.state('otherwise', {
      url: '*path',
      templateUrl: 'app/not-found.tpl.html',
      onEnter: ['$stateParams', function ($stateParams) {
        $ExceptionlessClient.createNotFound($stateParams.path)
          .setProperty('$stateParams', $stateParams)
          .submit();
      }]
    });
  })
  .run(function($http, $rootScope, $state, authService, editableOptions, $location, rateLimitService, Restangular, stateService, USE_SSL, $window) {
    if ($window.top.location.hostname !== $window.self.location.hostname){
      $window.top.location.href = $window.self.location.href;
    }

    if (((typeof USE_SSL === 'boolean' && USE_SSL) || USE_SSL === 'true') && $location.protocol() !== 'https') {
      $window.location.href = $location.absUrl().replace('http', 'https');
    }

    editableOptions.theme = 'bs3';
    Restangular.setErrorInterceptor(function(response, deferred, responseHandler) {
      function handleError(response) {
        if ($state.current.name !== 'status' && (response.status === 0 || response.status === 503)) {
          stateService.save(['auth.', 'status']);
          $state.go('status', { redirect: true });
          return true;
        }

        if (response.status === 401) {
          stateService.save(['auth.']);
          $state.go('auth.login');
          return true;
        }

        if (response.status === 409) {
          return true;
        }

        return false;
      }

      rateLimitService.updateFromResponseHeader(response);
      var currentState = $state.current.name;

      // Preserve original behavior on 401s on the auth pages... Logout could cause a 401.
      if (currentState && currentState.startsWith('auth.') && response.status === 401) {
        return true;
      }

      // Lets retry as long as we are not on the status page.
      if (currentState !== 'status' && (response.status === 0 || response.status === 503)) {
        // No request interceptors will be called on the retry.
        $http(response.config).then(responseHandler, function (response) {
          if (!handleError(response)) {
            deferred.reject(response);
          }
        });

        return false;
      }

      return !handleError(response);
    });

    $rootScope.page = {
      setTitle: function(title) {
        if (title) {
          this.title = title + ' - Exceptionless';
        } else {
          this.title = 'Exceptionless';
        }
      }
    };

    $rootScope.$on('$stateChangeStart', function (event, toState, toParams) {
      if (!toState || !toState.data || !toState.data.requireAuthentication)
        return;

      if (!authService.isAuthenticated()) {
        event.preventDefault();
        stateService.saveRequested(['auth.'], toState.name, toParams);
        $state.transitionTo('auth.login');
      }

      $rootScope.page.setTitle(toState.title);
    });
  });
}());
