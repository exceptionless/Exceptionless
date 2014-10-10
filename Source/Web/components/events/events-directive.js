(function() {
    'use strict';

    angular.module('exceptionless.events', ['exceptionless.link', 'exceptionless.summary', 'exceptionless.timeago'])
        .directive('events', function(linkService) {
            return {
                restrict: 'E',
                replace: true,
                scope: {
                    settings: "="
                },
                templateUrl: 'components/events/events-directive.tpl.html',
                controller: ['$rootScope', '$scope', '$window', '$state', 'linkService', function ($rootScope, $scope, $window, $state, linkService) {
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

                    var unbind = $rootScope.$on('eventOccurrence', function(e, data){
                        if (!vm.previous)
                            get(vm.settings.options);
                    });

                    $scope.$on('$destroy', unbind);

                    vm.hasEvents = hasEvents;
                    vm.open = open;
                    vm.nextPage = nextPage;
                    vm.previousPage = previousPage;

                    get(settings.options);
                }],
                controllerAs: 'vm'
            };
        });
}());
