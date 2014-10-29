(function() {
    'use strict';

    angular.module('exceptionless.events')
        .directive('events', function(linkService) {
            return {
                restrict: 'E',
                replace: true,
                scope: {
                    settings: "="
                },
                templateUrl: 'components/events/events-directive.tpl.html',
                controller: ['$rootScope', '$scope', '$window', '$state', 'linkService', 'notificationService', 'eventsActionsService', function ($rootScope, $scope, $window, $state, linkService, notificationService, eventsActionsService) {
                    var settings = $scope.settings;
                    var vm = this;

                    function get(options) {
                        settings.get(options).then(function (response) {
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
                            $state.go('app.event', { id: id });
                        }
                    }

                    function nextPage() {
                        get(vm.next);
                    }

                    function previousPage() {
                        get(vm.previous);
                    }

                    function updateSelection() {
                        if (!hasEvents())
                            return;

                        if (hasSelection())
                            vm.selectedIds = [];
                        else
                            vm.selectedIds = vm.events.map(function(event) { return event.id; });
                    }

                    function save() {
                        if (!hasSelection()) {
                            notificationService.info(null, 'Please select one or more events');
                            return;
                        }

                        if (!vm.selectedAction) {
                            notificationService.info(null, 'Please select a bulk action');
                            return;
                        }

                        vm.selectedAction.run(vm.selectedIds);
                    }

                    var unbind = $rootScope.$on('eventOccurrence', function(e, data){
                        if (!vm.previous)
                            get(vm.settings.options);
                    });

                    $scope.$on('$destroy', unbind);

                    vm.actions = eventsActionsService.getActions();
                    vm.hasEvents = hasEvents;
                    vm.hasSelection = hasSelection;
                    vm.open = open;
                    vm.nextPage = nextPage;
                    vm.previousPage = previousPage;
                    vm.save = save;
                    vm.selectedIds = [];
                    vm.selectedAction = null;
                    vm.updateSelection = updateSelection;

                    get(settings.options);
                }],
                controllerAs: 'vm'
            };
        });
}());
