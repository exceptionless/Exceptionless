(function () {
  'use strict';

  angular.module('exceptionless.event')
  .directive('extendedDataItem', [function () {
    return {
      restrict: 'E',
      scope: {
        canPromote: '=',
        data: '=',
        demoteTab: '&',
        excludedKeys: '=',
        isPromoted: '=',
        promoteTab: '&',
        title: '='
      },
      templateUrl: 'app/event/extended-data-item-directive.tpl.html',
      controller: ['$scope', function ($scope) {
        var vm = this;

        function demoteTab() {
          return $scope.demoteTab({ tabName: vm.title });
        }

        function getData(data, exclusions) {
          if (!exclusions || exclusions.length === 0) {
            return data;
          }

          var result = {};
          angular.forEach(data, function (value, key) {
            if (exclusions.indexOf(key) < 0) {
              result[key] = value;
            }
          }, result);

          return result;
        }

        function hasData() {
          return vm.data && !angular.equals({}, vm.data);
        }

        function promoteTab() {
          return $scope.promoteTab({ tabName: vm.title });
        }

        vm.canPromote = $scope.canPromote !== false;
        vm.data = getData($scope.data, $scope.excludedKeys);
        vm.demoteTab =  demoteTab;
        vm.hasData = hasData;
        vm.isPromoted =  $scope.isPromoted === true;
        vm.promoteTab = promoteTab;
        vm.showRaw = false;
        vm.title = $scope.title;
      }],
      controllerAs: 'vm'
    };
  }]);
}());
