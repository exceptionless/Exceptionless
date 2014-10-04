(function () {
    'use strict';

    angular.module('app.frequent')
        .controller('Frequent', ['stackService', function (stackService) {
            var vm = this;
            vm.mostFrequent = {
                header: 'Most Frequent',
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
