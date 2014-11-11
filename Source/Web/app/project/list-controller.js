(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.List', ['projectService', function (projectService) {
      var vm = this;
      vm.projects = {
        get: function (options) {
          return projectService.getAll(options);
        },
        options: {
          limit: 10,
          mode: 'summary'
        }
      };
    }
    ]);
}());
