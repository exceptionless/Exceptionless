(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.New', ['stackService', function (stackService) {
            var vm = this;
            vm.newest = {
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
