(function () {
  'use strict';

  angular.module('exceptionless.event')
  .directive('extendedDataItem', [function () {
    return {
      restrict: 'E',
      scope: {
        demoteTab: '&',
        isPromoted: '=',
        promoteTab: '&',
        tab: '=tabData'
      },
      templateUrl: 'app/event/extended-data-item-directive.tpl.html',
      controller: ['$scope', function ($scope) {
        var vm = this;

        function demoteTab() {
          return $scope.demoteTab({ tabName: vm.tab.title });
        }

        function promoteTab() {
          return $scope.promoteTab({ tabName: vm.tab.title });
        }

        vm.demoteTab =  demoteTab;
        vm.isPromoted =  $scope.isPromoted === true;
        vm.promoteTab = promoteTab;
        vm.showRaw = false;
        vm.tab = $scope.tab;
      }],
      controllerAs: 'vm'
    };
  }]);
}());
