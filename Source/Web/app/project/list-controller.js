(function () {
    'use strict';

    angular.module('app.project')
        .controller('List', ['projectService', function (projectService) {
            var vm = this;
            vm.projects = {
                header: 'My Projects',
                get: function (options) {
                    return projectService.getAll(options);
                },
                options: {
                    limit: 10,
                    mode: 'summary'
                }
            };

            console.log('blah');
        }
    ]);
}());
