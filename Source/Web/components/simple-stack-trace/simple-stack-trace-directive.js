(function() {
    'use strict';

    angular.module('exceptionless.simple-stack-trace', [
        'angular-filters',
        'exceptionless.simple-error'
    ])
    .directive('simpleStackTrace', ['simpleErrorService', function(simpleErrorService) {
        return {
            restrict: 'E',
            replace: true,
            scope: {
                exception: "="
            },
            templateUrl: 'components/simple-stack-trace/simple-stack-trace-directive.tpl.html',
            controller: ['$scope', function ($scope) {
                var vm = this;
                vm.exceptions = simpleErrorService.getExceptions($scope.exception);
            }],
            controllerAs: 'vm'
        };
    }]);
}());
