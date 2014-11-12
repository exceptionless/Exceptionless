(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .directive('projectFilter', function () {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/filter/project-filter-directive.tpl.html',
        controller: ['$state', 'filterService', 'notificationService', 'projectService', function ($state, filterService, notificationService, projectService) {
          function get() {
            function onSuccess(response) {
              vm.projects = response.data.plain();
              vm.filterName = getFilterName();
            }

            function onFailure() {
              notificationService.error('An error occurred while loading your projects.');
            }

            var options = {};
            return projectService.getAll(options).then(onSuccess, onFailure);
          }

          function getFilterName() {
            var organizationId = filterService.getOrganizationId();
            if (organizationId) {
              for (var index = 0; index < vm.projects.length; index++) {
                if (vm.projects[index].organization_id === organizationId) {
                  return vm.projects[index].organization_name;
                }
              }

              clearFilter();
            }

            var projectId = filterService.getProjectId();
            if (projectId) {
              for (var index2 = 0; index2 < vm.projects.length; index2++) {
                if (vm.projects[index2].id === projectId) {
                  return vm.projects[index2].name;
                }
              }

              clearFilter();
            }

            return 'All Projects';
          }

          function clearFilter() {
            filterService.clearOrganizationAndProjectFilter();
          }

          function setProject(project) {
            filterService.setProjectId(project.id);
          }

          function setOrganization(project) {
            filterService.setOrganizationId(project.organization_id);
          }

          var vm = this;
          vm.clearFilter = clearFilter;
          vm.filterName = 'Loading';
          vm.getFilterName = getFilterName;
          vm.projects = [];
          vm.setProject = setProject;
          vm.setOrganization = setOrganization;

          get();
        }],
        controllerAs: 'vm'
      };
    });
}());
