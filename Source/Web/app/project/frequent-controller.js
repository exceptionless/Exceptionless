(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Frequent', ['stackService', function (stackService) {
            var vm = this;
            vm.mostFrequent = {
                get: function (options) {
                    return stackService.getAll(options);
                },
                options: {
                    limit: 20,
                    mode: 'summary'
                }
            };
        }
    ]);
}());
