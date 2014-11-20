(function () {
  'use strict';

  angular.module('exceptionless.projects', [
    'exceptionless.dialog',
    'exceptionless.refresh',
    'exceptionless.link',
    'exceptionless.notification',
    'exceptionless.project'
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
        controller: ['$window', '$state', 'dialogService', 'linkService', 'notificationService', 'projectService', function ($window, $state, dialogService, linkService, notificationService, projectService) {
          var vm = this;

          function get(options) {
            vm.settings.get(options || vm.settings.options).then(function (response) {
              vm.projects = response.data.plain();

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];
            });
          }

          function hasProjects() {
            return vm.projects && vm.projects.length > 0;
          }

          function open(id, event) {
            // TODO: implement this.
            if (event.ctrlKey || event.which === 2) {
              $window.open('/#/project/' + id + '/manage', '_blank');
            } else {
              $state.go('app.project.manage', { id: id });
            }
          }

          function nextPage() {
            get(vm.next);
          }

          function previousPage() {
            get(vm.previous);
          }

          function remove(project) {
            return dialogService.confirmDanger('Are you sure you want to remove the project?', 'REMOVE PROJECT').then(function () {
              function onSuccess() {
                vm.projects.splice(vm.projects.indexOf(project), 1);
              }

              function onFailure() {
                notificationService.error('An error occurred while trying to remove the project.');
              }

              return projectService.remove(project.id).then(onSuccess, onFailure);
            });
          }

          vm.get = get;
          vm.hasProjects = hasProjects;
          vm.nextPage = nextPage;
          vm.open = open;
          vm.previousPage = previousPage;
          vm.projects = [];
          vm.remove = remove;

          get();
        }],
        controllerAs: 'vm'
      };
    });
}());
