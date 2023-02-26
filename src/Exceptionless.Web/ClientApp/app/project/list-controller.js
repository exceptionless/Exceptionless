(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.List', function (projectService) {
      var vm = this;

      function setSearchFilter(filter) {
        vm.projects.options.filter = filter || "";
      }

      vm.projects = {
        get: projectService.getAll,
        options: {
          filter: '',
          limit: 10,
          mode: 'stats'
        }
      };
      vm.setSearchFilter = setSearchFilter;
    });
}());
