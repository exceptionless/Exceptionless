(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Frequent', ['$stateParams', 'stackService', function ($stateParams, stackService) {
            var projectId = $stateParams.id;

            var vm = this;
            vm.mostFrequent = {
                get: function (options) {
                    return stackService.getFrequentByProjectId(projectId, options);
                },
                options: {
                    limit: 20,
                    mode: 'summary'
                }
            };
        }
    ]);
}());
