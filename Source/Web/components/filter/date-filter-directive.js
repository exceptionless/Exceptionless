(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .directive('dateFilter', function () {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/filter/date-filter-directive.tpl.html',
        controller: ['filterService', function (filterService) {
          var vm = this;

          function getFilters() {
            return [
              { key: 'last 24 hours', value: 'Last 24 Hours' },
              { key: 'last week', value: 'Last Week' },
              { key: 'last 30 days', value: 'Last 30 Days' },
              { key: 'all', value: 'All Time' },
              { key: 'custom', value: 'Custom' }
            ];
          }

          function hasFilter() {
            return filterService.getTime();
          }

          function setFilter(filter) {
            filterService.setTime(filter);
          }

          vm.hasFilter = hasFilter;
          //vm.range = getTimeRange();
          vm.isDropDownOpen = false;
          vm.setFilter = setFilter;
        }],
        controllerAs: 'vm'
      };
    });
}());
