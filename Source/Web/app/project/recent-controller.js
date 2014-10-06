(function () {
    'use strict';

    angular.module('app.project')
        .controller('Recent', ['eventService', function (eventService) {
            var vm = this;
            vm.mostRecent = {
                header: 'Most Recent',
                get: function (options) {
                    return eventService.getAll(options);
                },
                options: {
                    limit: 20,
                    mode: 'summary'
                }
            };
        }
    ]);
}());
