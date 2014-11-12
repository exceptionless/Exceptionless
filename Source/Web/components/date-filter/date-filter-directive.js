(function () {
  'use strict';

  angular.module('exceptionless.date-filter')
    .directive('dateFilter', ['$interval', function ($interval) {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/date-filter/date-filter-directive.tpl.html',
        controller: ['$interval', '$scope', 'dialogs', 'filterService', function ($interval, $scope, dialogs, filterService) {
          function getFilterName() {
            var time = filterService.getTime();
            if (time === 'last hour') {
              return moment().subtract('hours', 1).twix(moment()).format();
            }

            if (time === 'last 24 hours') {
              return moment().subtract('hours', 24).twix(moment()).format();
            }

            if (time === 'last week') {
              return moment().subtract('days', 7).startOf('day').twix(moment()).format();
            }

            if (time === 'last 30 days') {
              return moment().subtract('days', 30).startOf('day').twix(moment()).format();
            }

            if (time) {
              var range = time.split('-');
              if (range.length === 2) {
                return moment(range[0]).twix(moment(range[1])).format();
              } else {
                setFilter();
              }
            }

            return 'All Time';
          }

          function hasFilter() {
            return filterService.getTime();
          }

          function setCustomFilter() {
            function onSuccess(range) {
              console.log(range);
              setFilter();
            }

            dialogs.create('/components/date-filter/custom-date-range-dialog.tpl.html', 'CustomDateRangeDialog as vm').result.then(onSuccess);
          }

          function setFilter(filter) {
            filterService.setTime(filter);
          }

          function updateFilterName() {
            vm.filterName = getFilterName();
          }

          var interval = $interval(updateFilterName, 60 * 1000);
          $scope.$on('$destroy', function () {
            $interval.cancel(interval);
          });

          var vm = this;
          vm.hasFilter = hasFilter;
          vm.filterName = getFilterName();
          vm.isDropDownOpen = false;
          vm.setCustomFilter = setCustomFilter;
          vm.setFilter = setFilter;
          vm.updateFilterName = updateFilterName;
        }],
        controllerAs: 'vm'
      };
    }]);
}());
