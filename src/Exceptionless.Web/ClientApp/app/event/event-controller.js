(function () {
  'use strict';

  angular.module('app.event')
    .controller('Event', function ($ExceptionlessClient, $scope, $state, $stateParams, $timeout, $window, billingService, clipboard, dialogService, errorService, eventService, filterService, hotkeys, linkService, notificationService, projectService, simpleErrorService, urlService, translateService) {
      var vm = this;

      function activateSessionEventsTab() {
        activateTab(translateService.T('Session Events'));
      }

      function activateTab(tabName) {
        for(var index = 0; index < vm.tabs.length; index++) {
          var tab = vm.tabs[index];
          if (tab.title !== tabName) {
            tab.active = false;
            continue;
          }

          if (tab.template_key === 'session') {
            vm.sessionEventsTabActivated = true;
          }

          tab.active = true;
          vm.activeTabIndex = tab.index;
          break;
        }

        if (vm.activeTabIndex < 0 || vm.activeTabIndex >= vm.tabs.length) {
          vm.tabs[0].active = true;
          vm.activeTabIndex = 0;
        }
      }

      function addHotKeys() {
        hotkeys.del('mod+up');
        hotkeys.del('mod+left');
        hotkeys.del('mod+right');
        hotkeys.del('mod+shift+c');

        if (vm.event.stack_id) {
          hotkeys.bindTo($scope).add({
            combo: 'mod+up',
            description: translateService.T('Go To Stack'),
            callback: function () {
              $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys.GoToStack')
                .addTags('hotkeys')
                .setProperty('id', vm._eventId)
                .submit();

              $state.go('app.stack', {id: vm.event.stack_id});
            }
          });

          if (clipboard.supported) {
            hotkeys.bindTo($scope).add({
              combo: 'mod+shift+c',
              description: translateService.T('Copy Event JSON to Clipboard'),
              callback: function () {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys.CopyEventJSON')
                  .addTags('hotkeys')
                  .setProperty('id', vm._eventId)
                  .submit();

                clipboard.copyText(vm.event_json);
                copied();
              }
            });
          }
        }

        if (vm.previous) {
          hotkeys.bindTo($scope).add({
            combo: 'mod+left',
            description: translateService.T('Previous Occurrence'),
            callback: function () {
              $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys.PreviousOccurrence')
                .addTags('hotkeys')
                .setProperty('id', vm._eventId)
                .submit();

              $state.go('app.event', { id: vm.previous, tab: vm.getCurrentTab() });
            }
          });
        }

        if (vm.next) {
          hotkeys.bindTo($scope).add({
            combo: 'mod+right',
            description: translateService.T('Next Occurrence'),
            callback: function () {
              $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys.NextOccurrence')
                .addTags('hotkeys')
                .setProperty('id', vm._eventId)
                .submit();

              $state.go('app.event', { id: vm.next, tab: vm.getCurrentTab() });
            }
          });
        }

      }

      function buildReferences() {
        function toSpacedWords(value) {
          value = value.replace(/_/g, ' ').replace(/\s+/g, ' ').trim();
          value = value.replace(/([a-z0-9])([A-Z0-9])/g, '$1 $2');
          return value.length > 1 ? value.charAt(0).toUpperCase() + value.slice(1) : value;
        }

        vm.references = [];

        var referencePrefix = '@ref:';
        angular.forEach(vm.event.data, function (data, key) {
          if (key === '@ref:session') {
            vm.referenceId = data;
          }

          if (key.startsWith(referencePrefix)) {
            vm.references.push({ id: data, name: toSpacedWords(key.slice(5)) });
          }
        });
      }

      function buildTabs(tabNameToActivate) {
        var tabIndex = 0;
        var tabs = [{index: tabIndex, title: translateService.T('Overview'), template_key: 'overview'}];

        if (vm.event.data && vm.event.data['@error']) {
          tabs.push({index: ++tabIndex, title: translateService.T('Exception'), template_key: 'error'});
        } else if (vm.event.data && vm.event.data['@simple_error']) {
          tabs.push({index: ++tabIndex, title: translateService.T('Exception'), template_key: 'simple-error'});
        }

        if (vm.request && Object.keys(vm.request).length > 0) {
          tabs.push({index: ++tabIndex, title: translateService.T(vm.isSessionStart ? 'Browser' : 'Request'), template_key: 'request'});
        }

        if (vm.environment && Object.keys(vm.environment).length > 0) {
          tabs.push({index: ++tabIndex, title: translateService.T('Environment'), template_key: 'environment'});
        }

        var extendedDataItems = [];
        angular.forEach(vm.event.data, function(data, key) {
          if (key === '@trace') {
            key = 'Trace Log';
          }

          if (key.startsWith('@')) {
            return;
          }

          if (isPromoted(key)) {
            tabs.push({index: ++tabIndex, title: key, template_key: 'promoted', data: data});
          } else if (vm._knownDataKeys.indexOf(key) < 0) {
            extendedDataItems.push({title: key, data: data});
          }
        }, tabs);

        if (extendedDataItems.length > 0) {
          tabs.push({index: ++tabIndex, title: translateService.T('Extended Data'), template_key: 'extended-data', data: extendedDataItems});
        }

        if (vm.referenceId) {
          tabs.push({ index: ++tabIndex, title: translateService.T('Session Events'), template_key: 'session' });
        }

        vm.tabs = tabs;
        $timeout(function() { activateTab(tabNameToActivate); }, 1);
      }

      function canRefresh(data) {
        if (!!data && data.type === 'PersistentEvent') {
          // Refresh if the event id is set (non bulk) and the deleted event matches one of the events.
          if (data.id && vm.event.id) {
            return data.id === vm.event.id;
          }

          return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
        }

        if (!!data && data.type === 'Stack') {
          return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
        }

        if (!!data && data.type === 'Organization' || data.type === 'Project') {
          return filterService.includedInProjectOrOrganizationFilter({organizationId: data.id, projectId: data.id});
        }

        return !data;
      }

      function copied() {
        notificationService.success(translateService.T('Copied!'));
      }

      function demoteTab(tabName) {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteTab.success')
            .setProperty('id', vm._eventId)
            .setProperty('TabName', tabName)
            .submit();

          vm.project.promoted_tabs.splice(indexOf, 1);
          buildTabs(translateService.T('Extended Data'));
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteTab.error')
            .setProperty('id', vm._eventId)
            .setProperty('response', response)
            .setProperty('TabName', tabName)
            .submit();

          notificationService.error(translateService.T('An error occurred promoting tab.'));
        }

        var indexOf = vm.project.promoted_tabs.indexOf(tabName);
        if (indexOf < 0) {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.demoteTab')
          .setProperty('id', vm._eventId)
          .setProperty('TabName', tabName)
          .submit();

        return projectService.demoteTab(vm.project.id, tabName).then(onSuccess, onFailure);
      }

      function getCurrentTab() {
        var tab = vm.tabs.filter(function(t) { return t.index === vm.activeTabIndex; })[0];
        return tab && tab.index > 0 ? tab.title : translateService.T('Overview');
      }

      function getDuration() {
        // TODO: this binding expression can be optimized.
        return vm.event.value || moment().diff(vm.event.date, 'seconds');
      }

      function getEvent() {
        function optionsCallback(options) {
          if (options.filter) {
            options.filter += ' stack:current';
          } else {
            options.filter = 'stack:current';
          }

          return options;
        }

        function onSuccess(response) {
          function getExceptions(event) {
            var error = event.data && event.data['@error'];
            if (error) {
              return errorService.getExceptions(error);
            }

            var simpleError = event.data && event.data['@simple_error'];
            return simpleErrorService.getExceptions(simpleError);
          }

          function getErrorData(event) {
            var exceptions = getExceptions(event);
            return exceptions
              .map(function (ex, index, errors) {
                var errorType = ex.type || 'Unknown';
                return {
                  title: index === 0 ? 'Additional Data' : errorType + ' Additional Data',
                  type: errorType,
                  message: ex.message,
                  data: ex.data && ex.data['@ext']
                };
              })
              .filter(function (errorData) { return !!errorData.data; });
          }

          function getErrorType(event) {
            var error = event.data && event.data['@error'];
            if (error) {
              var type = errorService.getTargetInfoExceptionType(error);
              return type || error.type || 'Unknown';
            }

            var simpleError = event.data && event.data['@simple_error'];
            return (simpleError && simpleError.type) ? simpleError.type : 'Unknown';
          }

          function getLocation(event) {
            var location = event.data ? event.data['@location'] : null;
            if (!location) {
              return;
            }

            return [location.locality, location.level1, location.country]
              .filter(function(value) { return value && value.length; })
              .reduce(function(a, b, index) {
                a += (index > 0 ? ', ' : '') + b;
                return a;
              }, '');
          }

          function getMessage(event) {
            if (event.data && event.data['@error']) {
              var message = errorService.getTargetInfoMessage(event.data['@error']);
              if (message) {
                return message;
              }
            }

            return event.message;
          }

          vm.event = response.data.plain();
          vm.event_json = angular.toJson(vm.event, true);
          vm.sessionEvents.relativeTo = vm.event.date;
          vm.errorData = getErrorData(vm.event);
          vm.errorType = getErrorType(vm.event);
          vm.environment = vm.event.data && vm.event.data['@environment'];
          vm.location = getLocation(vm.event);
          vm.message = getMessage(vm.event);
          vm.hasError = vm.event.data && (vm.event.data['@error'] || vm.event.data['@simple_error']);
          vm.isSessionStart = vm.event.type === 'session';
          vm.referenceId = vm.isSessionStart ? vm.event.reference_id : null;
          vm.level = vm.event.data && !!vm.event.data['@level'] ? vm.event.data['@level'].toLowerCase() : null;
          vm.isLevelSuccess = vm.level === 'trace' || vm.level === 'debug';
          vm.isLevelInfo = vm.level === 'info';
          vm.isLevelWarning = vm.level === 'warn';
          vm.isLevelError = vm.level === 'error';

          vm.request = vm.event.data && vm.event.data['@request'];
          vm.hasCookies = vm.request && !!vm.request.cookies && Object.keys(vm.request.cookies).length > 0;
          vm.requestUrl = vm.request && urlService.buildUrl(vm.request.is_secure, vm.request.host, vm.request.port, vm.request.path, vm.request.query_string);

          vm.user = vm.event.data && vm.event.data['@user'];
          vm.userIdentity = vm.user && vm.user.identity;
          vm.userName = vm.user && vm.user.name;

          vm.userDescription = vm.event.data && vm.event.data['@user_description'];
          vm.userEmail = vm.userDescription && vm.userDescription.email_address;
          vm.userDescription = vm.userDescription && vm.userDescription.description;
          vm.version = vm.event.data && vm.event.data['@version'];

          var links = linkService.getLinks(response.headers('link'));
          vm.previous = links['previous'] ? links['previous'].split('/').pop() : null;
          vm.next = links['next'] ? links['next'].split('/').pop() : null;

          addHotKeys();
          buildReferences();

          return vm.event;
        }

        function onFailure(response) {
          if (response && response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message).then(function () {
                return getEvent();
              }, function () {
                $state.go('app.frequent');
              }
            );
          }

          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Event', { eventId : $stateParams.id }));
        }

        if (!vm._eventId) {
          onFailure();
        }

        return eventService.getById(vm._eventId, {}, optionsCallback).then(onSuccess, onFailure).catch(function (e) {});
      }

      function getProject() {
        function onSuccess(response) {
          vm.project = response.data.plain();
          vm.project.promoted_tabs = vm.project.promoted_tabs || [];

          return vm.project;
        }

        function onFailure() {
          $state.go('app.frequent');
        }

        if (!vm.event || !vm.event.project_id) {
          onFailure();
        }

        return projectService.getById(vm.event.project_id, true).then(onSuccess, onFailure);
      }

      function isPromoted(tabName) {
        if (!vm.project || !vm.project.promoted_tabs) {
          return false;
        }

        return vm.project.promoted_tabs.filter(function (tab) { return tab === tabName; }).length > 0;
      }

      function promoteTab(tabName) {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteTab.success')
            .setProperty('id', vm._eventId)
            .setProperty('TabName', tabName)
            .submit();

          vm.project.promoted_tabs.push(tabName);
          buildTabs(tabName);
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteTab.error')
            .setProperty('id', vm._eventId)
            .setProperty('response', response)
            .setProperty('TabName', tabName)
            .submit();

          notificationService.error(translateService.T('An error occurred promoting tab.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteTab')
          .setProperty('id', vm._eventId)
          .setProperty('TabName', tabName)
          .submit();

        return projectService.promoteTab(vm.project.id, tabName).then(onSuccess, onFailure);
      }

      function viewJSON() {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.viewJSON.success')
            .setProperty('id', vm._eventId)
            .submit();
        }

        function onFailure() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.viewJSON.error')
            .setProperty('id', vm._eventId)
            .submit();
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.viewJSON')
          .setProperty('id', vm._eventId)
          .submit();

        return dialogService.viewJSON(vm.event_json).then(onSuccess, onFailure);
      }

      function updateIsAccordionVisible() {
        vm.isAccordionVisible = $window.innerWidth < 768;
      }

      this.$onInit = function $onInit() {
        updateIsAccordionVisible();
        var window = angular.element($window);
        window.bind('resize', updateIsAccordionVisible);

        var unbind = $scope.$on('$destroy', function () {
          unbind();
          window.unbind('resize', updateIsAccordionVisible);
        });

        vm._source = 'app.event.Event';
        vm._eventId = $stateParams.id;
        vm._knownDataKeys = ['error', '@error', '@simple_error', '@request', '@trace', '@environment', '@user', '@user_description', '@version', '@level', '@location', '@submission_method', '@submission_client', 'session_id', 'sessionend', 'haserror', '@stack'];

        vm.activeTabIndex = -1;
        vm.activateTab = activateTab;
        vm.canRefresh = canRefresh;
        vm.copied = copied;
        vm.demoteTab = demoteTab;
        vm.event = {};
        vm.event_json = '';
        vm.activateSessionEventsTab = activateSessionEventsTab;
        vm.textStackTrace = '';
        vm.excludedAdditionalData = ['@browser', '@browser_version', '@browser_major_version', '@device', '@os', '@os_version', '@os_major_version', '@is_bot'];
        vm.getCurrentTab = getCurrentTab;
        vm.getDuration = getDuration;
        vm.errorData = [];
        vm.errorType = 'Unknown';
        vm.environment = {};
        vm.location = '';
        vm.message = '';
        vm.isSessionStart = false;
        vm.level = '';
        vm.isLevelSuccess = false;
        vm.isLevelInfo = false;
        vm.isLevelWarning = false;
        vm.isLevelError = false;
        vm.isPromoted = isPromoted;
        vm.request = {};
        vm.requestUrl = '';
        vm.hasCookies = false;
        vm.hasError = false;
        vm.user = {};
        vm.userIdentity = '';
        vm.userName = '';
        vm.userDescription = {};
        vm.userEmail = '';
        vm.userDescription = '';
        vm.version = '';
        vm.project = {};
        vm.promoteTab = promoteTab;
        vm.references = [];
        vm.referenceId = null;
        vm.sessionEvents = {
          eventId: vm._eventId,
          canRefresh: function canRefresh(events, data) {
            if (data.type === 'PersistentEvent') {
              // We are already listening to the stack changed event... This prevents a double refresh.
              if (!data.deleted) {
                return false;
              }

              // Refresh if the event id is set (non bulk) and the deleted event matches one of the events.
              if (!!data.id && !!events) {
                return events.filter(function (e) { return e.id === data.id; }).length > 0;
              }

              return data.project_id ? vm.event.project_id === data.project_id : vm.event.organization_id === data.organization_id;
            }

            if (data.type === 'Stack') {
              return data.project_id ? vm.event.project_id === data.project_id : vm.event.organization_id === data.organization_id;
            }

            if (data.type === 'Project') {
              return vm.event.project_id === data.id;
            }

            return data.type === 'Organization' && vm.event.organization_id === data.id;
          },
          get: function (options) {
            function optionsCallback(options) {
              options.filter = '-type:heartbeat';

              var start = vm.isSessionStart ? moment.utc(vm.event.date).local() : moment.utc(vm.event.date).subtract(180, 'days').local();
              var end = (vm.event.data && vm.event.data.sessionend) ? moment.utc(vm.event.data.sessionend).add(1, 'seconds').local().format('YYYY-MM-DDTHH:mm:ss') : 'now';
              options.time = start.format('YYYY-MM-DDTHH:mm:ss') + '-' + end;

              return options;
            }

            return eventService.getBySessionId(vm.event.project_id, vm.referenceId, options, optionsCallback);
          },
          options: {
            limit: 10,
            mode: 'summary'
          },
          source: vm._source + '.Events',
          timeHeaderText: 'Session Time',
          hideActions: true,
          hideSessionStartTime: true
        };
        vm.sessionEventsTabActivated = false;
        vm.viewJSON = viewJSON;
        vm.tabs = [];

        return getEvent().then(getProject).then(function () {
          buildTabs($stateParams.tab);
        });
      };
    });
}());
