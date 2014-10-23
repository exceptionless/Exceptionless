(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Dashboard', ['$stateParams', 'eventService', 'notificationService', 'stackService', 'statService', function ($stateParams, eventService, notificationService, stackService, statService) {
            var projectId = $stateParams.id;

            function getStats() {
                function onSuccess(response) {
                    vm.stats = response.data.plain();
                }

                function onFailure() {
                    notificationService.error('An error occurred while loading the stats for your project.');
                }

                var options = {};
                return statService.getByProjectId(projectId, options).then(onSuccess, onFailure);
            }

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
            vm.stats = {};

            getStats();
        }
    ]);
}());
