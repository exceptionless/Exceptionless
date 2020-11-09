(function () {
  'use strict';

  angular.module('exceptionless.stacks')
    .directive('stacks', function () {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: '='
        },
        templateUrl: 'components/stacks/stacks-directive.tpl.html',
        controller: function ($ExceptionlessClient, $window, $state, $stateParams, linkService, filterService, notificationService, paginationService, stacksActionsService, translateService) {
          var vm = this;
          function canRefresh(data) {
            if (!!data && data.type === 'Stack') {
              return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
            }

            if (!!data && data.type === 'Organization' || data.type === 'Project') {
              return filterService.includedInProjectOrOrganizationFilter({organizationId: data.id, projectId: data.id});
            }

            return !data;
          }

          function get(options) {
            function onSuccess(response) {
              vm.stacks = response.data.plain();
              vm.selectedIds = vm.selectedIds.filter(function(id) { return vm.stacks.filter(function(e) { return e.id === id; }).length > 0; });

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];

              vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

              if (vm.stacks.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
                return get();
              }

              return vm.stacks;
            }

            function onFailure(response) {
              if (response.status !== 404 && response.data) {
                notificationService.error("Error loading stacks: " + (response.data.message || response.data));
              }

              $ExceptionlessClient.createLog(vm._source + '.get', 'Error while loading stacks', 'Error').setProperty('options', options).setProperty('response', response).submit();
              vm.stacks = [];
              vm.previous = null;
              vm.next = null;
              return response;
            }

            vm.loading = vm.stacks.length === 0;
            vm.currentOptions = options || vm.settings.options;
            return vm.settings.get(vm.currentOptions).then(onSuccess, onFailure).finally(function() {
              vm.loading = false;
            });
          }

          function open(id, event) {
            var openInNewTab = (event.ctrlKey || event.metaKey || event.which === 2);
            $ExceptionlessClient.createFeatureUsage(vm._source + '.open').setProperty('id', id).setProperty('_blank', openInNewTab).submit();
            if (openInNewTab) {
              $window.open($state.href('app.stack', { id: id }, { absolute: true }), '_blank');
            } else {
              $state.go('app.stack', { id: id });
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
              notificationService.info(null, translateService.T('Please select one or more stacks'));
            } else {
              action.run(vm.selectedIds).then(onSuccess);
            }
          }

          function updateSelection() {
            if (vm.stacks && vm.stacks.length === 0)
              return;

            if (vm.selectedIds.length > 0)
              vm.selectedIds = [];
            else
              vm.selectedIds = vm.stacks.map(function (stack) {
                return stack.id;
              });
          }

          this.$onInit = function $onInit() {
            vm._source = vm.settings.source + '.stacks';
            vm.actions = stacksActionsService.getActions();
            vm.canRefresh = canRefresh;
            vm.get = get;
            vm.hasFilter = filterService.hasFilter;
            vm.loading = true;
            vm.nextPage = nextPage;
            vm.open = open;
            vm.previousPage = previousPage;
            vm.save = save;
            vm.selectedIds = [];
            vm.showStatus = vm.settings.summary ? vm.settings.showStatus : !filterService.getStatus();
            vm.showType = vm.settings.summary ? vm.settings.showType : !filterService.getEventType();
            vm.stacks = [];
            vm.updateSelection = updateSelection;

            get();
          };
        },
        controllerAs: 'vm'
      };
    });
}());
