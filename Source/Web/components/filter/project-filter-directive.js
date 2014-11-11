(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .directive('projectFilter', function () {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/filter/project-filter-directive.tpl.html',
        controller: ['$state', 'filterService', 'projectService', function ($state, filterService, projectService) {
          function get() {
            function onSuccess(response) {
              vm.projects = response.data.plain();
            }

            function onFailure() {
              notificationService.error('An error occurred while loading your projects.');
            }

            var options = {};
            return projectService.getAll(options).then(onSuccess, onFailure);
          }

          function setProject(filter) {
            filterService.setProject(filter);
          }

          var vm = this;
          vm.projects = [];
          vm.setProject = setProject;

          get();
        }],
        controllerAs: 'vm'
      };
    });
}());
