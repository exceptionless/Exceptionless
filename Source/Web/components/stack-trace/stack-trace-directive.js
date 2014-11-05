(function() {
    'use strict';

    angular.module('exceptionless.stack-trace', [
        'angular-filters',
        'exceptionless.error'
    ])
    .directive('stackTrace', ['errorService', function(errorService) {
        return {
            bindToController: true,
            restrict: 'E',
            replace: true,
            scope: {
                exception: "="
            },
            templateUrl: 'components/stack-trace/stack-trace-directive.tpl.html',
            controller: [function () {
                var vm = this;
                vm.exceptions = errorService.getExceptions(vm.exception);
            }],
            controllerAs: 'vm'
        };
    }]);
}());
