(function() {
    'use strict';

    angular.module('exceptionless.stack-trace', [
        'angular-filters',
        'exceptionless.exception'
    ])
    .directive('stackTrace', ['exceptionService', function(exceptionService) {
        return {
            restrict: 'E',
            replace: true,
            scope: {
                exception: "="
            },
            templateUrl: 'components/stack-trace/stack-trace-directive.tpl.html',
            controller: ['$scope', function ($scope) {
                var vm = this;
                vm.exceptions = exceptionService.getExceptions($scope.exception);
            }],
            controllerAs: 'vm'
        };
    }]);
}());
