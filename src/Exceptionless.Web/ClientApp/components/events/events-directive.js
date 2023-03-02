(function () {
  'use strict';

  angular.module('exceptionless.events')
    .directive('events', function (linkService) {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: '='
        },
        templateUrl: 'components/events/events-directive.tpl.html',
        controller: ['$ExceptionlessClient', '$window', '$state', '$stateParams', '$translate', 'eventsActionsService', 'filterService', 'linkService', 'notificationService', 'paginationService', 'translateService', function ($ExceptionlessClient, $window, $state, $stateParams, $translate, eventsActionsService, filterService, linkService, notificationService, paginationService, translateService) {
          var vm = this;
          function afterRelativeText(ev) {
            return moment(ev.date).diff(vm.settings.relativeTo, 'milliseconds') >= 0 ? $translate.instant('after') + ' ' : '';
          }

          function beforeRelativeText(ev) {
            return moment(ev.date).diff(vm.settings.relativeTo, 'milliseconds') < 0 ? ' ' + $translate.instant('before') : '';
          }

          function canRefresh(data) {
            if (vm.refreshing || !data) {
              return false;
            }

            var settingsCanRefresh = vm.settings.canRefresh;
            if (settingsCanRefresh) {
              return settingsCanRefresh(vm.events, data);
            }

            if (data.type === 'PersistentEvent') {
              // We are already listening to the stack changed event... This prevents a double refresh.
              if (!data.deleted) {
                return false;
              }

              // Refresh if the event id is set (non bulk) and the deleted event matches one of the events.
              if (!!data.id && !!vm.events) {
                return vm.events.filter(function (e) { return e.id === data.id; }).length > 0;
              }

              return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
            }

            if (data.type === 'Stack') {
              return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
            }

            if (data.type === 'Organization' || data.type === 'Project') {
              return filterService.includedInProjectOrOrganizationFilter({organizationId: data.id, projectId: data.id});
            }

            return !data;
          }

          function get(options) {
            function onSuccess(response) {
              vm.events = response.data.plain();
              vm.selectedIds = vm.selectedIds.filter(function(id) { return vm.events.filter(function(e) { return e.id === id; }).length > 0; });

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];

              vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

              if (vm.events.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
                return get();
              }

              return vm.events;
            }

            function onFailure(response) {
              if (response.status !== 404 && response.data) {
                notificationService.error("Error loading events: " + (response.data.message || response.data));
              }

              $ExceptionlessClient.createLog(vm._source + '.get', 'Error while loading events', 'Error').setProperty('options', options).setProperty('response', response).submit();
              vm.events = [];
              vm.previous = null;
              vm.next = null;
              return response;
            }

            vm.refreshing = true;
            vm.loading = vm.events.length === 0;
            vm.currentOptions = options || vm.settings.options;
            return vm.settings.get(vm.currentOptions).then(onSuccess, onFailure).finally(function() {
              vm.refreshing = false;
              vm.loading = false;
            });
          }

          function open(id, event) {
            var openInNewTab = (event.ctrlKey || event.metaKey || event.which === 2);
            $ExceptionlessClient.createFeatureUsage(vm._source + '.open').setProperty('id', id).setProperty('_blank', openInNewTab).submit();
            if (openInNewTab) {
              $window.open($state.href('app.event', { id: id }, { absolute: true }), '_blank');
            } else {
              $state.go('app.event', { id: id });
            }

            event.preventDefault();
          }

          function nextPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.nextPage').setProperty('next', vm.next).submit();
            return get(vm.next);
          }

          function previousPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.previousPage').setProperty('previous', vm.previous).submit();
            return get(vm.previous);
          }

          function save(action) {
            function onSuccess() {
              vm.selectedIds = [];
            }

            if (vm.selectedIds.length === 0) {
              notificationService.info(null, translateService.T('Please select one or more events'));
            } else {
              action.run(vm.selectedIds).then(onSuccess);
            }
          }

          function updateSelection() {
            if (vm.events && vm.events.length === 0)
              return;

            if (vm.selectedIds.length > 0)
              vm.selectedIds = [];
            else
              vm.selectedIds = vm.events.map(function (event) {
                return event.id;
              });
          }

          function toggleDateSort() {
            vm.settings.sortByDateDescending = !vm.sortByDateDescending;
            vm.sortByDateDescending = vm.settings.sortByDateDescending;

            var options = vm.currentOptions;
            var sortPrefix = vm.sortByDateDescending ? "-" : "+";
            options.sort = sortPrefix + "date";

            get(options);
          }

          this.$onInit = function $onInit() {
            vm._source = vm.settings.source + '.events';
            vm.actions = vm.settings.hideActions ? [] : eventsActionsService.getActions();
            vm.afterRelativeText = afterRelativeText;
            vm.beforeRelativeText = beforeRelativeText;
            vm.canRefresh = canRefresh;
            vm.currentEventId = vm.settings.eventId;
            vm.events = [];
            vm.get = get;
            vm.hasFilter = filterService.hasFilter;
            vm.hideSessionStartTime = vm.settings.hideSessionStartTime || false;
            vm.refreshing = true;
            vm.loading = true;
            vm.open = open;
            vm.nextPage = nextPage;
            vm.previousPage = previousPage;
            vm.timeHeaderText = vm.settings.timeHeaderText || 'Date';
            vm.relativeTo = function() { return vm.settings.relativeTo; };
            vm.toggleDateSort = toggleDateSort;
            vm.sortByDateDescending = vm.settings.sortByDateDescending === undefined ? true : vm.sortByDateDescending;
            vm.save = save;
            vm.selectedIds = [];
            vm.showType = vm.settings.summary ? vm.settings.summary.showType : !filterService.getEventType();
            vm.showIPAddress = vm.settings.summary ? vm.settings.summary.showIPAddress : filterService.getEventType() === '404';
            vm.updateSelection = updateSelection;
            get();
          };
        }],
        controllerAs: 'vm'
      };
    });
}());
