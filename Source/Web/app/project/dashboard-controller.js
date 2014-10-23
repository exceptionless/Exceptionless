(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Dashboard', ['$stateParams', 'eventService', 'stackService', function ($stateParams, eventService, stackService) {
            var projectId = $stateParams.id;

            var vm = this;
            vm.mostFrequent = {
                get: function (options) {
                    return stackService.getFrequentByProjectId(projectId, options);
                },
                options: {
                    limit: 5,
                    mode: 'summary'
                }
            };

            vm.mostRecent = {
                header: 'Most Recent',
                get: function (options) {
                    return eventService.getAll(options);
                },
                options: {
                    limit: 5,
                    mode: 'summary'
                }
            };
        }
    ]);
}());
