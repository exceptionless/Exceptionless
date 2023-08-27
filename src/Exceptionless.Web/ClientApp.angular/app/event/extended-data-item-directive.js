(function () {
    "use strict";

    angular.module("exceptionless.event").directive("extendedDataItem", [
        function () {
            return {
                restrict: "E",
                scope: {
                    canPromote: "=",
                    data: "=",
                    demoteTab: "&",
                    excludedKeys: "=",
                    isPromoted: "=",
                    promoteTab: "&",
                    title: "=",
                },
                templateUrl: "app/event/extended-data-item-directive.tpl.html",
                controller: function ($scope, notificationService, translateService) {
                    var vm = this;
                    function copied() {
                        notificationService.success(translateService.T("Copied!"));
                    }

                    function demoteTab() {
                        return $scope.demoteTab({ tabName: vm.title });
                    }

                    function getData(data, exclusions) {
                        exclusions = exclusions && exclusions.length ? exclusions : [];
                        if (typeof data !== "object" || !(data instanceof Object)) {
                            return data;
                        }

                        return Object.keys(data)
                            .filter(function (value) {
                                return value && value.length && exclusions.indexOf(value) < 0;
                            })
                            .map(function (value) {
                                return { key: value, name: value };
                            })
                            .sort(function (a, b) {
                                return a.name - b.name;
                            })
                            .reduce(function (a, b) {
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
                        vm.data = $scope.data;
                        vm.hasData = typeof vm.data !== "undefined" && !angular.equals({}, vm.data);
                        vm.data_json = vm.hasData ? angular.toJson(vm.data) : "";
                        vm.filteredData = getData(vm.data, $scope.excludedKeys);
                        vm.hasFilteredData =
                            typeof vm.filteredData !== "undefined" && !angular.equals({}, vm.filteredData);
                        vm.isPromoted = $scope.isPromoted === true;
                        vm.promoteTab = promoteTab;
                        vm.showRaw = false;
                        vm.title = translateService.T($scope.title);
                    };
                },
                controllerAs: "vm",
            };
        },
    ]);
})();
