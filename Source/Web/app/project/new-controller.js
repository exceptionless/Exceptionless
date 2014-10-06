(function () {
    'use strict';

    angular.module('app.project')
        .controller('New', ['stackService', function (stackService) {
            var vm = this;
            vm.newest = {
                header: 'Newest Events',
                headerIcon: 'fa-asterisk',
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
