(function() {
    'use strict';

    angular.module('exceptionless.stacks', [
        'checklist-model',
        'exceptionless.link',
        'exceptionless.notification',
        'exceptionless.stack-actions',
        'exceptionless.summary',
        'exceptionless.timeago'
    ])
    .directive('stacks', function() {
        return {
            restrict: 'E',
            replace: true,
            scope: {
                settings: "="
            },
            templateUrl: 'components/stacks/stacks-directive.tpl.html',
            controller: ['$rootScope', '$scope', '$window', '$state', 'linkService', 'notificationService', 'stackActionsService', function ($rootScope, $scope, $window, $state, linkService, notificationService, stackActionsService) {
                var settings = $scope.settings;
                var vm = this;

                function get(options) {
                    settings.get(options).then(function (response) {
                        vm.selectedIds = [];
                        vm.stacks = response.data.plain();

                        var links = linkService.getLinksQueryParameters(response.headers('link'));
                        vm.previous = links['previous'];
                        vm.next = links['next'];
                    });
                }

                function hasStacks() {
                    return vm.stacks && vm.stacks.length > 0;
                }

                function hasSelection() {
                    return vm.selectedIds.length > 0;
                }

                function open(id, event) {
                    if (event.ctrlKey || event.which === 2) {
                        $window.open('/#/app/stack/' + id, '_blank');
                    } else {
                        $state.go('app.stack', { id: id });
                    }
                }

                function updateSelection() {
                    if (!hasStacks())
                        return;

                    if (hasSelection())
                        vm.selectedIds = [];
                    else
                        vm.selectedIds = vm.stacks.map(function(stack) { return stack.id; });
                }

                function save() {
                    if (!hasSelection()) {
                        notificationService.info(null, 'Please select one or more stacks');
                        return;
                    }

                    if (!vm.selectedAction) {
                        notificationService.info(null, 'Please select a bulk action');
                        return;
                    }

                    // TODO: This needs to support bulk operations.
                    for (var i = 0; i < vm.selectedIds.length; i++){
                        vm.selectedAction.run(vm.selectedIds[i]);
                    }
                }

                function nextPage() {
                    get(vm.next);
                }

                function previousPage() {
                    get(vm.previous);
                }

                var unbind = $rootScope.$on('eventOccurrence', function(e, data){
                    if ($scope.previous === undefined)
                        get($scope.settings.options);
                });

                $scope.$on('$destroy', unbind);

                vm.actions = stackActionsService.getActions();
                vm.hasStacks = hasStacks;
                vm.hasSelection = hasSelection;
                vm.header = settings.header;
                vm.nextPage = nextPage;
                vm.open = open;
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
