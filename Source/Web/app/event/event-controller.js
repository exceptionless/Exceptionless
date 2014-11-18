(function () {
  'use strict';

  angular.module('app.event')
    .controller('Event', ['$state', '$stateParams', 'errorService', 'eventService', 'notificationService', 'projectService', 'urlService', 'userAgentService', function ($state, $stateParams, errorService, eventService, notificationService, projectService, urlService, userAgentService) {
      var eventId = $stateParams.id;
      var vm = this;

      function buildTabs() {
        var tabs = [{title: 'Overview', template_key: 'overview'}];

        if (isError()) {
          if (vm.event.data.error) {
            tabs.push({title: 'Exception', template_key: 'error'});
          } else if (vm.event.data.simple_error) {
            tabs.push({title: 'Exception', template_key: 'simple-error'});
          }
        }

        if (hasRequestInfo()) {
          tabs.push({title: 'Request', template_key: 'request'});
        }

        if (hasEnvironmentInfo()) {
          tabs.push({title: 'Environment', template_key: 'environment'});
        }

        var extendedDataItems = [];
        angular.forEach(vm.event.data, function(data, key) {
          if (isPromoted(key)) {
            tabs.push({ title: key, template_key: 'promoted', data: data });
          } else {
            extendedDataItems.push({title: key, data: data });
          }
        }, tabs);

        if (extendedDataItems.length > 0) {
          tabs.push({title: 'Extended Data', template_key: 'extended-data', data: extendedDataItems});
        }

        vm.tabs = tabs;
      }

      function getEvent() {
        function onSuccess(response) {
          vm.event = response.data.plain();
          return vm.event;
        }

        function onFailure() {
          $state.go('app.dashboard');
          notificationService.error('The event "' + $stateParams.id + '" could not be found.');
        }

        if (!eventId) {
          onFailure();
        }

        return eventService.getById(eventId).then(onSuccess, onFailure);
      }

      function getProject() {
        function onSuccess(project) {
          vm.project = project;
          vm.project.promoted_tabs.push('JsonDataFromConfig');
          return vm.project;
        }

        function onFailure() {
          $state.go('app.dashboard');
        }

        if (!vm.event || !vm.event.project_id) {
          onFailure();
        }

        return projectService.getById(vm.event.project_id).then(onSuccess, onFailure);
      }

      function getBrowser() {
        return userAgentService.getBrowser(vm.event.data.request.user_agent);
      }

      function getBrowserOS() {
        return userAgentService.getBrowserOS(vm.event.data.request.user_agent);
      }

      function getDevice() {
        return userAgentService.getDevice(vm.event.data.request.user_agent);
      }

      function getErrorType() {
        if (vm.event.data.error) {
          return errorService.getTargetMethodType(vm.event.data.error);
        }

        if (vm.event.data.simple_error) {
          return vm.event.data.simple_error.type;
        }

        return 'Unknown';
      }

      function getMessage() {
        if (vm.event.data.error) {
          return errorService.getTargetException(vm.event.data.error).message;
        }

        return vm.event.message;
      }

      function getRequestUrl() {
        var request = vm.event.data.request;
        return urlService.buildUrl(request.is_secure, request.host, request.port, request.path, request.query_string);
      }

      function hasCookies() {
        return Object.keys(vm.event.data.request.cookies).length > 0;
      }

      function hasDevice() {
        return hasUserAgent() && getDevice();
      }

      function hasEnvironmentInfo() {
        return vm.event.data && vm.event.data.environment;
      }

      function hasIdentity() {
        return vm.event.data && vm.event.data.user && vm.event.data.user.identity;
      }

      function hasIPAddress() {
        return hasRequestInfo() && vm.event.data.request.client_ip_address && vm.event.data.request.client_ip_address.length > 0;
      }

      function hasReferrer() {
        return vm.event.data && vm.event.data.request && vm.event.data.request.referrer;
      }

      function hasRequestInfo() {
        return vm.event.data && vm.event.data.request;
      }

      function hasUserAgent() {
        return vm.event.data && vm.event.data.request && vm.event.data.request.user_agent;
      }

      function hasUserEmail() {
        return vm.event.data && vm.event.data.user_description && vm.event.data.user_description.email_address;
      }

      function hasUserDescription() {
        return vm.event.data && vm.event.data.user_description && vm.event.data.user_description.description;
      }

      function hasTags() {
        return vm.event.tags && vm.event.tags.length > 0;
      }

      function isError() {
        return vm.event.type === 'error';
      }

      function isPromoted(tabName) {
        if (!vm.project || !vm.project.promoted_tabs) {
          return false;
        }

        return vm.project.promoted_tabs.filter(function (tab) { return tab === tabName; }).length > 0;
      }

      vm.event = {};
      vm.getBrowser = getBrowser;
      vm.getBrowserOS = getBrowserOS;
      vm.getDevice = getDevice;
      vm.getErrorType = getErrorType;
      vm.getMessage = getMessage;
      vm.getRequestUrl = getRequestUrl;
      vm.hasCookies = hasCookies;
      vm.hasDevice = hasDevice;
      vm.hasIdentity = hasIdentity;
      vm.hasReferrer = hasReferrer;
      vm.hasRequestInfo = hasRequestInfo;
      vm.hasTags = hasTags;
      vm.hasUserAgent = hasUserAgent;
      vm.hasUserDescription = hasUserDescription;
      vm.hasUserEmail = hasUserEmail;
      vm.isError = isError;
      vm.tabs = [];

      getEvent().then(getProject).then(buildTabs);
    }]);
}());
