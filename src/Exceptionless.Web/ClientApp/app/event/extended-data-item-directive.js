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
      controller: function ($scope, notificationService, translateService) {
        var vm = this;
        function copied() {
          notificationService.success(translateService.T('Copied!'));
        }

        function demoteTab() {
          return $scope.demoteTab({ tabName: vm.title });
        }

        function getData(data, exclusions) {
          function toSpacedWords(value) {
            value = value.replace(/_/g, ' ').replace(/\s+/g, ' ').trim();
            value = value.replace(/([a-z0-9])([A-Z0-9])/g, '$1 $2');
            return value.length > 1 ? value.charAt(0).toUpperCase() + value.slice(1) : value;
          }

          exclusions = exclusions && exclusions.length ? exclusions : [];

          if (typeof data !== 'object' || !(data instanceof Object)) {
            return data;
          }

          return Object.keys(data)
            .filter(function(value) { return value && value.length && exclusions.indexOf(value) < 0; })
            .map(function(value) { return { key: value, name: toSpacedWords(value) }; })
            .sort(function(a, b) { return a.name - b.name; })
            .reduce(function(a, b) {
              a[b.name] = data[b.key];
              return a;
            }, {});
        }

        function promoteTab() {
          return $scope.promoteTab({ tabName: vm.title });
        }

        this.$onInit = function $onInit() {
          vm.copied = copied;
          vm.canPromote = $scope.canPromote !== false;
          vm.demoteTab = demoteTab;
          vm.data = getData($scope.data, $scope.excludedKeys);
          vm.hasData = typeof vm.data !== 'undefined' && !angular.equals({}, vm.data);
          vm.data_json = (vm.hasData ? angular.toJson(vm.data) : '');
          vm.isPromoted = $scope.isPromoted === true;
          vm.promoteTab = promoteTab;
          vm.showRaw = false;
          vm.title = translateService.T($scope.title);
        };
      },
      controllerAs: 'vm'
    };
  }]);
}());
