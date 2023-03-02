(function () {
  'use strict';

  angular.module('exceptionless.project-filter', [
    'angular.filter',

    'exceptionless.auto-active',
    'exceptionless.organization',
    'exceptionless.project',
    'exceptionless.refresh',
    'exceptionless.show-on-hover-parent'
  ])
  .directive('projectFilter', [function () {
    return {
      restrict: 'E',
      replace: true,
      scope: true,
      templateUrl: 'components/project-filter/project-filter-directive.tpl.html',
      controller: function ($rootScope, $scope, $state, $stateParams, $window, debounce, filterService, notificationService, organizationService, projectService, urlService, translateService) {
        var vm = this;
        function buildUrls() {
          function getOrganizationUrl(organization) {
            if (isOnSessionEvents()) {
              return urlService.buildFilterUrl({ route: getStateName(), routePrefix: 'session', organizationId: organization.id });
            } else if (isOnReports()) {
              return urlService.buildFilterUrl({ moduleName: 'app.reports', route: 'status', organizationId: organization.id }, { status: filterService.getStatus() });
            }

            return urlService.buildFilterUrl({ route: getStateName(), organizationId: organization.id, type: $stateParams.type });
          }

          function getAllProjectsUrl() {
            if (isOnSessionEvents()) {
              return urlService.buildFilterUrl({ route: getStateName(), routePrefix: 'session' });
            } else if (isOnReports()) {
              return urlService.buildFilterUrl({ moduleName: 'app.reports', route: 'status' }, { status: filterService.getStatus() });
            }

            return urlService.buildFilterUrl({ route: getStateName(), type: $stateParams.type });
          }

          function getProjectUrl(project) {
            if (isOnSessionEvents()) {
              return urlService.buildFilterUrl({ route: getStateName(), routePrefix: 'session', projectId: project.id });
            } else if (isOnReports()) {
              return urlService.buildFilterUrl({ moduleName: 'app.reports', route: 'status', projectId: project.id }, { status: filterService.getStatus() });
            }

            return urlService.buildFilterUrl({ route: getStateName(), projectId: project.id, type: $stateParams.type });
          }

          var result = {
            organizations: {},
            projects: {
              all: getAllProjectsUrl()
            }
          };

          vm.organizations.forEach(function(organization) {
            result.organizations[organization.id] = getOrganizationUrl(organization);
          });

          vm.projects.forEach(function(project) {
            result.projects[project.id] = getProjectUrl(project);
          });

          return result;
        }

        function get() {
          return getOrganizations().then(getProjects);
        }

        function getFilterName() {
          var organizationId = filterService.getOrganizationId();
          if (organizationId) {
            var organization = vm.organizations.filter(function(o) { return o.id === organizationId; })[0];
            if (organization) {
              return organization.name;
            }
          }

          var projectId = filterService.getProjectId();
          if (projectId) {
            var project = vm.projects.filter(function(p) { return p.id === projectId; })[0];
            if (project) {
              return project.name;
            }
          }

          return translateService.T('All Projects');
        }

        function getOrganizations() {
          function onSuccess(response) {
            vm.organizations = response.data.plain();
            if (filterService.getOrganizationId() && !vm.organizations.filter(function(o) { return o.id === filterService.getOrganizationId(); })) {
              filterService.setOrganizationId();
            }

            update();
            vm.isLoadingOrganizations = false;
            return response;
          }

          function onFailure() {
            vm.isLoadingOrganizations = false;
            notificationService.error(translateService.T('An error occurred while loading your organizations.'));
          }

          return organizationService.getAll().then(onSuccess, onFailure);
        }

        function getProjects() {
          function onSuccess(response) {
            vm.projects = response.data.plain();
            if (filterService.getProjectId() && !vm.projects.filter(function(p) { return p.id === filterService.getProjectId(); })) {
              filterService.setProjectId();
            }

            update();
            vm.isLoadingProjects = false;
            return response;
          }

          function onFailure() {
            vm.isLoadingProjects = false;
            notificationService.error(translateService.T('An error occurred while loading your projects.'));
          }

          return projectService.getAll().then(onSuccess, onFailure);
        }

        function getFilteredOrganizations() {
          var filter = vm.filter && vm.filter.toLocaleLowerCase();

          return vm.organizations.filter(function (organization) {
            if (!filter || organization.name.toLocaleLowerCase().includes(filter))
              return organization;

            var hasProjectMatchingFilter = vm.projects.find(function (p) {
              return p.organization_id === organization.id && p.name.toLocaleLowerCase().includes(filter);
            });

            if (hasProjectMatchingFilter)
              return organization;

            return null;
          });
        }

        function getFilteredProjectsByOrganizationId(id) {
          var filter = vm.filter && vm.filter.toLocaleLowerCase();

          return vm.projects.filter(function (project) {
            if (project.organization_id !== id)
              return null;

            if (!filter || project.name.toLocaleLowerCase().includes(filter) || project.organization_name.toLocaleLowerCase().includes(filter))
              return project;

            return null;
          });
        }

        function getStateName() {
          if ($state.current.name.endsWith('frequent')) {
            return 'frequent';
          }

          if ($state.current.name.endsWith('new')) {
            return 'new';
          }

          if ($state.current.name.endsWith('users')) {
            return 'users';
          }

          return 'events';
        }

        function isOnSessionEvents() {
          return $state.current.name.contains('app.session-') || $state.current.name === 'app.session.events';
        }

        function isOnReports() {
          return $state.current.name.contains('app.reports.');
        }

        function showSearch() {
          return vm.projects.length >= 20 || vm.organizations.length >= 20;
        }

        function update() {
          vm.filteredDisplayName = getFilterName();
          vm.urls = buildUrls();
        }

        this.$onInit = function $onInit() {
          var updateFilterDropDownMaxHeight = debounce(function() {
            vm.filterDropDownMaxHeight = angular.element($window).height() - 100;
          }, 150);

          var window = angular.element($window);
          window.bind('resize', updateFilterDropDownMaxHeight);

          $rootScope.$on('$stateChangeSuccess', update);
          var unbind = $scope.$on('$destroy', function() {
            unbind();
            window.unbind('resize', updateFilterDropDownMaxHeight);
          });

          vm.filteredDisplayName = 'Loading';
          vm.get = get;
          vm.getFilteredOrganizations = getFilteredOrganizations;
          vm.getFilteredProjectsByOrganizationId = getFilteredProjectsByOrganizationId;
          vm.filter = "";
          vm.isLoadingOrganizations = true;
          vm.isLoadingProjects = true;
          vm.organizations = [];
          vm.projects = [];
          vm.showSearch = showSearch;
          vm.urls = buildUrls();
          vm.update = update;

          updateFilterDropDownMaxHeight();
          get();
        };
      },
      controllerAs: 'vm'
    };
  }]);
}());
