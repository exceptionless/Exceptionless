(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.New', ['$stateParams', 'stackService', function ($stateParams, stackService) {
            var projectId = $stateParams.id;

            var vm = this;
            vm.newest = {
                get: function (options) {
                    return stackService.getNewByProjectId(projectId, options);
                },
                options: {
                    limit: 20,
                    mode: 'summary'
                }
            };
        }
    ]);
}());
