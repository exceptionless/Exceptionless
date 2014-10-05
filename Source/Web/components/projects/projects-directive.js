(function() {
    'use strict';

    angular.module('exceptionless.projects', [
        'exceptionless.link',
        'exceptionless.notification'
    ])
    .directive('projects', function() {
        return {
            restrict: 'E',
            replace: true,
            scope: {
                settings: "="
            },
            templateUrl: 'components/projects/projects-directive.tpl.html',
            controller: ['$rootScope', '$scope', '$window', '$state', 'linkService', 'notificationService', function ($rootScope, $scope, $window, $state, linkService, notificationService) {
                var settings = $scope.settings;
                var vm = this;

                function get(options) {
                    settings.get(options).then(function (response) {
                        vm.selectedIds = [];
                        vm.projects = response.data.plain();

                        var links = linkService.getLinksQueryParameters(response.headers('link'));
                        vm.previous = links['previous'];
                        vm.next = links['next'];
                    });
                }

                function hasProjects() {
                    return vm.projects && vm.projects.length > 0;
                }

                function open(id, event) {
                    // TODO: implement this.
                    if (event.ctrlKey || event.which === 2) {
                        $window.open('/#/app/dashboard/' + id, '_blank');
                    } else {
                        $state.go('app.dashboard', { id: id });
                    }
                }

                function nextPage() {
                    get(vm.next);
                }

                function previousPage() {
                    get(vm.previous);
                }

                var unbind = $rootScope.$on('ProjectChanged', function(e, data){
                    if ($scope.previous === undefined)
                        get($scope.settings.options);
                });

                $scope.$on('$destroy', unbind);

                vm.hasProjects = hasProjects;
                vm.header = settings.header;
                vm.headerIcon = settings.headerIcon || 'fa-briefcase';
                vm.nextPage = nextPage;
                vm.open = open;
                vm.previousPage = previousPage;

                get(settings.options);
            }],
            controllerAs: 'vm'
        };
    });
}());
