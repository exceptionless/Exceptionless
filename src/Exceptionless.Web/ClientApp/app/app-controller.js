(function () {
  'use strict';

  angular.module('app')
    .controller('App', function ($rootScope, $scope, $state, $stateParams, $window, authService, billingService, $ExceptionlessClient, filterService, hotkeys, INTERCOM_APPID, $intercom, locker, notificationService, organizationService, websocketService, stateService, statusService, SLACK_APPID, STRIPE_PUBLISHABLE_KEY, urlService, userService, translateService) {
      var vm = this;
      function addHotkeys() {
        function logFeatureUsage(name) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys' + name).addTags('hotkeys').submit();
        }

        if (isIntercomEnabled()) {
          hotkeys.bindTo($scope)
            .add({
              combo: 'c',
              description: translateService.T('Chat with Support'),
              callback: function chatWithSupport() {
                logFeatureUsage('Support');
                showIntercom();
              }
            });
        }

        hotkeys.bindTo($scope)
          .add({
            combo: 'g w',
            description: translateService.T('Go To Documentation'),
            callback: function goToDocumention() {
              logFeatureUsage('Documentation');
              $window.open('https://exceptionless.com/docs/', '_blank');
            }
          })
          .add({
            combo: 's',
            description: translateService.T('Focus Search Bar'),
            callback: function focusSearchBar(event) {
              event.preventDefault();

              logFeatureUsage('SearchBar');
              $('#search').focus().select();
            }
          })
          .add({
            combo: 'g a',
            description: translateService.T('Go To My Account'),
            callback: function goToMyAccount() {
              logFeatureUsage('Account');
              $state.go('app.account.manage', { tab: 'general' });
            }
          })
          .add({
            combo: 'g n',
            description: translateService.T('Go To Notifications'),
            callback: function goToNotifications() {
              logFeatureUsage('Notifications');
              $state.go('app.account.manage', { tab: 'notifications' });
            }
          })
          .add({
            combo: 'g d',
            description: translateService.T('Go To Most Frequent'),
            callback: function goToMostFrequent() {
              logFeatureUsage('Most Frequent');
              $window.open(vm.eventsUrl.all, '_self');
            }
          })
          .add({
            combo: 'g o',
            description: translateService.T('Go To Organizations'),
            callback: function goToOrganizations() {
              logFeatureUsage('Organizations');
              $state.go('app.organization.list');
            }
          })
          .add({
            combo: 'g p',
            description: translateService.T('Go To Projects'),
            callback: function goToProjects() {
              logFeatureUsage('Projects');
              $state.go('app.project.list');
            }
          })
          .add({
            combo: 'g+g',
            description: translateService.T('Go To GitHub project'),
            callback: function goToGitHub() {
              logFeatureUsage('GitHub');
              $window.open('https://github.com/exceptionless/Exceptionless', '_blank');
            }
          })
          .add({
            combo: 'g s',
            description: translateService.T('Go to public Discord channel'),
            callback: function goToDiscord() {
              logFeatureUsage('Discord');
              $window.open('https://discord.gg/6HxgFCx', '_blank');
            }
          });
      }

      function buildMenus() {
        function getFilterUrl(route, type) {
          return urlService.buildFilterUrl({ route: route, projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId(), type: type });
        }

        function buildUrls() {
          var result = {
            events: {},
            frequent: {},
            users: {},
            new: {},
            reports: {}
          };

          [undefined, 'error', 'log', '404', 'usage'].forEach(function(type) {
            var key = !type ? 'all' : type;
            result.events[key] = getFilterUrl('events', type);
            result.frequent[key] = getFilterUrl('frequent', type);
            result.users[key] = getFilterUrl('users', type);
            result.new[key] = getFilterUrl('new', type);
          });

          result.reports.sessions = urlService.buildFilterUrl({ route: 'events', routePrefix: 'session', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId() });
          ['regressed', 'fixed', 'snoozed', 'ignored', 'discarded'].forEach(function(status) {
            result.reports[status] = urlService.buildFilterUrl({ moduleName: 'app.reports', route: 'status', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId() }, { status: status });
          });

          return result;
        }

        function isAllMenuActive(state, params) {
          return dashboards.filter(function(dashboard) {
            if (state.includes('app.' + dashboard, params) ||
              state.includes('app.project-' + dashboard, params) ||
              state.includes('app.organization-' + dashboard, params)) {
              return true;
            }

            return false;
          }).length > 0;
        }

        function isSettingsMenuActive(state, params) {
          return state.includes('app.project.list', params) ||
            state.includes('app.organization.list', params) ||
            state.includes('app.account.manage', params) ||
            state.includes('app.admin.dashboard', params);
        }

        function isReportsMenuActive(state, params) {
          return state.includes('app.session.events', params) ||
            state.includes('app.session-events', params) ||
            state.includes('app.session-project-events', params) ||
            state.includes('app.session-organization-events', params) ||
            state.current.name.contains('app.reports.');
        }

        function isTypeMenuActive(state, params, type) {
          var parameters = angular.extend({}, params, { type: type });
          return dashboards.filter(function(dashboard) {
            if (state.includes('app.type-' + dashboard, parameters) ||
              state.includes('app.project-type-' + dashboard, parameters) ||
              state.includes('app.organization-type-' + dashboard, parameters)) {
              return true;
            }

            return false;
          }).length > 0;
        }

        var dashboards = ['frequent', 'new', 'users', 'events'];
        vm.urls = buildUrls();

        var isMenuActive = {
          all: isAllMenuActive($state, $stateParams),
          error: isTypeMenuActive($state, $stateParams, 'error'),
          log: isTypeMenuActive($state, $stateParams, 'log'),
          '404': isTypeMenuActive($state, $stateParams, '404'),
          usage: isTypeMenuActive($state, $stateParams, 'usage'),
          settings: isSettingsMenuActive($state, $stateParams),
          reports: isReportsMenuActive($state, $stateParams)
        };

        var hasActiveMenu = Object.keys(isMenuActive).some(function(prop) { return isMenuActive[prop]; });
        if (hasActiveMenu) {
          vm.isMenuActive = isMenuActive;
        } else if (Object.keys(vm.isMenuActive).length === 0) {
          isMenuActive.all = true;
          vm.isMenuActive = isMenuActive;
        }
      }

      function changePlan(organizationId) {
        if (!STRIPE_PUBLISHABLE_KEY) {
          notificationService.error(translateService.T('Billing is currently disabled.'));
          return;
        }

        return billingService.changePlan(organizationId).catch(function(e){});
      }

      function getApiVersion() {
        function onSuccess(response) {
          var aboutResponse = response.data.plain();
          vm.apiVersionNumber = aboutResponse && aboutResponse.informational_version.split("+")[0];
          return response;
        }

        return statusService.about().then(onSuccess);
      }

      function getOrganizations() {
        function onSuccess(response) {
          vm.organizations = response.data.plain();
          vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organizations.length > 0;
          return response;
        }

        return organizationService.getAll().then(onSuccess);
      }

      function getUser(data) {
        function onSuccess(response) {
          vm.user = response.data.plain();
          $ExceptionlessClient.config.setUserIdentity({ identity: vm.user.email_address, name: vm.user.full_name, data: { user: vm.user }});
          return response;
        }

        if (data && data.type === 'User' && data.deleted && data.id === vm.user.id) {
          notificationService.error(translateService.T('Your user account was deleted. Please create a new account.'));
          return authService.logout(true);
        }

        return userService.getCurrentUser().then(onSuccess);
      }

      function isIntercomEnabled() {
        return authService.isAuthenticated() && INTERCOM_APPID;
      }

      function startWebSocket() {
        return websocketService.startDelayed(1000);
      }

      function showIntercom() {
        if (!isIntercomEnabled()) {
          return;
        }

        $ExceptionlessClient.submitFeatureUsage(vm._source + '.showIntercom');
        $intercom.showNewMessage();
      }

      function toggleSideNavCollapsed() {
        vm.isSideNavCollapsed = !vm.isSideNavCollapsed;
        vm._store.put('sideNavCollapsed', vm.isSideNavCollapsed);
      }

      this.$onInit = function $onInit() {
        function isSmartDevice($window) {
          var ua = $window.navigator.userAgent || $window.navigator.vendor || $window.opera;
          return (/iPhone|iPod|iPad|Silk|Android|BlackBerry|Opera Mini|IEMobile/).test(ua);
        }

        if (!!navigator.userAgent.match(/MSIE/i)) {
          angular.element($window.document.body).addClass('ie');
        }

        if (isSmartDevice($window)) {
          angular.element($window.document.body).addClass('smart');
        }

        $rootScope.$on('$stateChangeSuccess', buildMenus);
        $scope.$on('$destroy', websocketService.stop);
        vm._source = 'app.App';
        vm._store = locker.driver('local').namespace('app');

        vm.apiVersionNumber = "";
        vm.canChangePlan = false;
        vm.changePlan = changePlan;
        vm.urls = {
          events: {},
          frequent: {},
          users: {},
          new: {},
          reports: {}
        };
        vm.getOrganizations = getOrganizations;
        vm.getUser = getUser;
        vm.isMenuActive = {};
        vm.isIntercomEnabled = isIntercomEnabled;
        vm.isSlackEnabled = !!SLACK_APPID;
        vm.isSideNavCollapsed = vm._store.get('sideNavCollapsed') === true;
        vm.organizations = [];
        vm.showIntercom = showIntercom;
        vm.toggleSideNavCollapsed = toggleSideNavCollapsed;
        vm.user = {};

        addHotkeys();
        buildMenus();
        getUser().then(getOrganizations).then(startWebSocket).then(getApiVersion);
      };
    });
}());
