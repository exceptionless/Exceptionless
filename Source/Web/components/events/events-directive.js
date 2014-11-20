(function () {
  'use strict';

  angular.module('exceptionless.events')
    .directive('events', function (linkService) {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: "="
        },
        templateUrl: 'components/events/events-directive.tpl.html',
        controller: ['$window', '$state', '$stateParams', 'linkService', 'notificationService', 'eventsActionsService', function ($window, $state, $stateParams, linkService, notificationService, eventsActionsService) {
          var vm = this;

          function get(options) {
            vm.settings.get(options || vm.settings.options).then(function (response) {
              vm.events = response.data.plain();

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];
            });
          }

          function hasEvents() {
            return vm.events && vm.events.length > 0;
          }

          function hasSelection() {
            return vm.selectedIds.length > 0;
          }

          function open(id, event) {
            if (event.ctrlKey || event.which === 2) {
              $window.open('/#/app/event/' + id, '_blank');
            } else {
              $state.go('app.event', {id: id});
            }
          }

          function nextPage() {
            get(vm.next);
          }

          function previousPage() {
            get(vm.previous);
          }

          function save(action) {
            if (!hasSelection()) {
              notificationService.info(null, 'Please select one or more events');
            } else {
              action.run(vm.selectedIds);
            }
          }

          function updateSelection() {
            if (!hasEvents())
              return;

            if (hasSelection())
              vm.selectedIds = [];
            else
              vm.selectedIds = vm.events.map(function (event) {
                return event.id;
              });
          }

          vm.actions = eventsActionsService.getActions();
          vm.get = get;
          vm.hasEvents = hasEvents;
          vm.hasSelection = hasSelection;
          vm.open = open;
          vm.nextPage = nextPage;
          vm.previousPage = previousPage;
          vm.save = save;
          vm.selectedIds = [];
          vm.showType = !$stateParams.type;
          vm.updateSelection = updateSelection;

          get();
        }],
        controllerAs: 'vm'
      };
    });
}());
