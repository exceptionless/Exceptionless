(function () {
  'use strict';

  angular.module('exceptionless.projects', [
    'exceptionless',
    'exceptionless.dialog',
    'exceptionless.filter',
    'exceptionless.link',
    'exceptionless.notification',
    'exceptionless.pagination',
    'exceptionless.project',
    'exceptionless.refresh',
    'exceptionless.validators'
  ])
    .directive('projects', function () {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: "="
        },
        templateUrl: 'components/projects/projects-directive.tpl.html',
        controller: function ($ExceptionlessClient, $window, $scope, $state, dialogService, filterService, linkService, notificationService, paginationService, projectService, translateService) {
          var vm = this;

          function get(options, useCache) {
            function onSuccess(response) {
              vm.projects = response.data.plain();

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];

              vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

              if (vm.projects.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
                return get(null, useCache);
              }

              return vm.projects;
            }

            function onFailure() {
              vm.projects = [];
              vm.previous = null;
              vm.next = null;
              vm.pageSummary = paginationService.getCurrentPageSummary(vm.projects, vm.currentOptions.page, vm.currentOptions.limit);

              return vm.projects;
            }

            vm.loading = vm.projects.length === 0;
            vm.currentOptions = options || vm.settings.options;
            return vm.settings.get(vm.currentOptions, useCache).then(onSuccess, onFailure).finally(function() {
              vm.loading = false;
            });
          }

          function hasProjects() {
            return vm.projects && vm.projects.length > 0;
          }

          function canRefresh(data) {
            if (!data || !data.type) {
              return true;
            }

            var organizationId = vm.settings.organization;
            if (data.type === 'Organization') {
              if (!hasProjects() && data.id === organizationId) {
                return true;
              }

              return vm.projects.filter(function (e) { return data.id === e.organization_id; }).length > 0;
            }

            if (data.type === 'Project') {
              if (!hasProjects() && data.organization_id === organizationId) {
                return true;
              }

              return vm.projects.filter(function (e) {
                  if (data.id) {
                    return data.id === e.id;
                  } else {
                    return data.organization_id = e.organization_id;
                  }
                }).length > 0;
            }

            if ((data.type === 'PersistentEvent' && !data.updated)) {
              if (!hasProjects() && data.organization_id === organizationId) {
                return true;
              }

              return vm.projects.filter(function (e) {
                  if (data.project_id) {
                    return data.project_id === e.id;
                  } else {
                    return data.organization_id = e.organization_id;
                  }
                }).length > 0;
            }

            return false;
          }

          function open(id, event) {
            var openInNewTab = (event.ctrlKey || event.metaKey || event.which === 2);
            $ExceptionlessClient.createFeatureUsage(vm._source + '.open').setProperty('id', id).setProperty('_blank', openInNewTab).submit();
            if (openInNewTab) {
              $window.open($state.href('app.project.manage', { id: id }, { absolute: true }), '_blank');
            } else {
              $state.go('app.project.manage', { id: id });
            }

            event.preventDefault();
          }

          function nextPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.nextPage').setProperty('next', vm.next).submit();
            return get(vm.next);
          }

          function previousPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.previousPage').setProperty('previous', vm.previous).submit();
            return get(vm.previous);
          }

          function remove(project) {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove').setProperty('project', project).submit();
            return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this project?'), translateService.T('Delete Project')).then(function () {
              function onSuccess() {
                vm.projects.splice(vm.projects.indexOf(project), 1);
                notificationService.info(translateService.T('Successfully queued the project for deletion.'));
                $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.success').setProperty('project', project).submit();
              }

              function onFailure() {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.error').setProperty('project', project).submit();
                notificationService.error(translateService.T('An error occurred while trying to remove the project.'));
              }

              return projectService.remove(project.id).then(onSuccess, onFailure);
            }).catch(function(e){});
          }

          this.$onInit = function $onInit() {
            vm._source = 'exceptionless.projects';
            vm.form = {};
            vm.canRefresh = canRefresh;
            vm.currentOptions = {};
            vm.get = get;
            vm.hasFilter = filterService.hasFilter;
            vm.hasProjects = hasProjects;
            vm.includeOrganizationName = !vm.settings.organization;
            vm.loading = true;
            vm.nextPage = nextPage;
            vm.open = open;
            vm.previousPage = previousPage;
            vm.projects = [];
            vm.remove = remove;

            $ExceptionlessClient.submitFeatureUsage(vm._source);
            get();
          };

          var watcher = $scope.$watch("vm.settings.options.filter", function (newValue, oldValue) {
            if (newValue !== oldValue) {
              vm.get();
            }
          });

          $scope.$on('$destroy', function () {
            watcher();
          });
        },
        controllerAs: 'vm'
      };
    });
}());
