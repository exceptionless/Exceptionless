(function() {
    'use strict';

    angular.module('exceptionless.simple-stack-trace', [
        'angular-filters',
        'exceptionless.simple-error'
    ])
    .directive('simpleStackTrace', ['simpleErrorService', function(simpleErrorService) {
        return {
            bindToController: true,
            restrict: 'E',
            replace: true,
            scope: {
                exception: "="
            },
            templateUrl: 'components/simple-stack-trace/simple-stack-trace-directive.tpl.html',
            controller: [function () {
                var vm = this;
                vm.exceptions = simpleErrorService.getExceptions(vm.exception);
            }],
            controllerAs: 'vm'
        };
    }]);
}());
