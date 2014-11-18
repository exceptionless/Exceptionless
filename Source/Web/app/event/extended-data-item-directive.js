(function () {
  'use strict';

  angular.module('exceptionless.event')
  .directive('extendedDataItem', [function () {
    return {
      restrict: 'E',
      scope: {
        tabData: '=',
        project: '='
      },
      templateUrl: 'app/event/extended-data-item-directive.tpl.html',
      controller: ['$scope', 'notificationService', 'projectService', function ($scope, notificationService, projectService) {
        var vm = this;

        function canBePromoted() {
          return true;
        }

        function demote() {
          function onSuccess(response) {
            vm.project.promoted_tabs.splice(indexOf, 1);
          }

          function onFailure() {
            notificationService.error('An error occurred promoting tab.');
          }

          var indexOf = vm.project.promoted_tabs.indexOf(vm.tab.title);
          if (indexOf < 0) {
            return;
          }

          return projectService.promoteTab(vm.project.id, vm.tab.title).then(onSuccess, onFailure);
        }

        function isPromoted() {
          if (!vm.project || !vm.project.promoted_tabs || !vm.tab) {
            return false;
          }

          return vm.project.promoted_tabs.filter(function (tab) { return tab === vm.tab.name; }).length > 0;
        }

        function promote() {
          function onSuccess(response) {
            vm.project.promoted_tabs.push(vm.tab.title);
          }

          function onFailure() {
            notificationService.error('An error occurred promoting tab.');
          }

          return projectService.promoteTab(vm.project.id, vm.tab.title).then(onSuccess, onFailure);
        }

        vm.canBePromoted = canBePromoted;
        vm.demote = demote;
        vm.isPromoted = isPromoted;
        vm.project = $scope.project;
        vm.promote = promote;
        vm.showRaw = false;
        vm.tab = $scope.tabData;
      }],
      controllerAs: 'vm'
    };
  }]);
}());
