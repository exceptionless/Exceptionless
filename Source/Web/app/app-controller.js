(function () {
  'use strict';

  angular.module('app')
    .controller('App', ['$state', '$stateParams', '$window', 'BASE_URL', 'filterService', 'signalRService', 'urlService', 'VERSION', function ($state, $stateParams, $window, BASE_URL, filterService, signalRService, urlService, VERSION) {
      function isSmartDevice($window) {
        var ua = $window['navigator']['userAgent'] || $window['navigator']['vendor'] || $window['opera'];
        return (/iPhone|iPod|iPad|Silk|Android|BlackBerry|Opera Mini|IEMobile/).test(ua);
      }

      function getDashboardUrl(type){
        return urlService.buildFilterUrl({ route: 'dashboard', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId(),  type: type });
      }

      function getRecentUrl(type){
        return urlService.buildFilterUrl({ route: 'recent', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId(),  type: type });
      }

      function getFrequentUrl(type){
        return urlService.buildFilterUrl({ route: 'frequent', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId(),  type: type });
      }

      function getNewUrl(type){
        return urlService.buildFilterUrl({ route: 'new', projectId: filterService.getProjectId(), organizationId: filterService.getOrganizationId(),  type: type });
      }

      function isAllMenuActive() {
        return $state.includes('app.dashboard', $stateParams) ||
          $state.includes('app.project-dashboard', $stateParams) ||
          $state.includes('app.organization-dashboard', $stateParams) ||
          $state.includes('app.frequent', $stateParams) ||
          $state.includes('app.project-frequent', $stateParams) ||
          $state.includes('app.organization-frequent', $stateParams) ||
          $state.includes('app.new', $stateParams) ||
          $state.includes('app.project-new', $stateParams) ||
          $state.includes('app.organization-new', $stateParams) ||
          $state.includes('app.recent', $stateParams) ||
          $state.includes('app.project-recent', $stateParams) ||
          $state.includes('app.organization-recent', $stateParams);
      }

      function isAdminMenuActive() {
        return $state.includes('app.project.list', $stateParams) ||
          $state.includes('app.organization.list', $stateParams) ||
          $state.includes('app.account.manage', $stateParams) ||
          $state.includes('app.admin.dashboard', $stateParams);
      }

      function isTypeMenuActive(type) {
        var params = angular.extend({}, $stateParams, { type: type });

        return $state.includes('app.type-dashboard', params) ||
          $state.includes('app.project-type-dashboard', params) ||
          $state.includes('app.organization-type-dashboard', params) ||
          $state.includes('app.type-frequent', params) ||
          $state.includes('app.project-type-frequent', params) ||
          $state.includes('app.organization-type-frequent', params) ||
          $state.includes('app.type-new', params) ||
          $state.includes('app.project-type-new', params) ||
          $state.includes('app.organization-type-new', params) ||
          $state.includes('app.type-recent', params) ||
          $state.includes('app.project-type-recent', params) ||
          $state.includes('app.organization-type-recent', params);
      }

      if (!!navigator.userAgent.match(/MSIE/i))
        angular.element($window.document.body).addClass('ie');

      if (isSmartDevice($window))
        angular.element($window.document.body).addClass('smart');

      // NOTE: we don't dispose of the SignalR timeout because it should never be disposed..
      signalRService.startDelayed(BASE_URL, 'd795c4406f6b4bc6ae8d787c65d0274d');

      var vm = this;
      vm.getDashboardUrl = getDashboardUrl;
      vm.getRecentUrl = getRecentUrl;
      vm.getFrequentUrl = getFrequentUrl;
      vm.getNewUrl = getNewUrl;
      vm.isAllMenuActive = isAllMenuActive;
      vm.isAdminMenuActive = isAdminMenuActive;
      vm.isTypeMenuActive = isTypeMenuActive;
      vm.project = {id: '537650f3b77efe23a47914f4'};
      vm.settings = {
        headerFixed: true,
        asideFixed: true,
        asideFolded: false
      };
      vm.user = {
        name: 'Eric J. Smith',
        avatar_url: '//www.gravatar.com/avatar/3f307a0eedda99476af09a6568c16c14.png'
      };
      vm.version = VERSION;
    }]);
}());
