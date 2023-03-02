(function () {
  'use strict';

  angular.module('exceptionless.date-filter')
    .directive('dateFilter', function ($interval, dateRangeParserService) {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/date-filter/date-filter-directive.tpl.html',
        controller: function ($interval, $scope, dialogs, filterService, translateService) {
          var vm = this;
          function getFilteredDisplayName() {
            var time = filterService.getTime();
            if (time === 'last hour') {
              return 'Last Hour';
            }

            if (time === 'last 24 hours') {
              return 'Last 24 Hours';
            }

            if (time === 'last week') {
              return 'Last Week';
            }

            if (time === 'last 30 days') {
              return 'Last 30 Days';
            }

            if (time === 'all') {
              return 'All Time';
            }

            var range = dateRangeParserService.parse(time);
            if (range && range.start && range.end) {
              return moment(range.start).twix(moment(range.end)).format({dayFormat:translateService.T('DayFormat')});
            }

            setFilter('last week');
            return 'Last Week';
          }

          function isActive(timeRangeName) {
            var time = filterService.getTime();
            if (time && timeRangeName === 'Custom') {
              var range = dateRangeParserService.parse(time);
              return range && range.start && range.end;
            }

            return timeRangeName === time;
          }

          function hasFilter() {
            return filterService.getTime() !== 'all';
          }

          function setCustomFilter() {
            function onSuccess(range) {
              setFilter(range.start.format('YYYY-MM-DDTHH:mm:ss') + '-' + range.end.format('YYYY-MM-DDTHH:mm:ss'));
              return range;
            }

            var range = filterService.getTimeRange();
            return dialogs.create('components/date-filter/custom-date-range-dialog.tpl.html', 'CustomDateRangeDialog as vm', { start: (range.start || moment().subtract(7, 'days')), end: (range.end || moment()) }).result.then(onSuccess).catch(function(e){});
          }

          function setFilter(filter) {
            filterService.setTime(filter);
          }

          function updateFilterDisplayName() {
            vm.filteredDisplayName = getFilteredDisplayName();
          }

          this.$onInit = function $onInit() {
            var interval = $interval(updateFilterDisplayName, 60 * 1000);
            $scope.$on('$destroy', function () {
              $interval.cancel(interval);
            });

            vm.hasFilter = hasFilter;
            vm.isActive = isActive;
            vm.filteredDisplayName = getFilteredDisplayName();
            vm.isDropDownOpen = false;
            vm.setCustomFilter = setCustomFilter;
            vm.setFilter = setFilter;
            vm.updateFilterDisplayName = updateFilterDisplayName;
          };
        },
        controllerAs: 'vm'
      };
    });
}());
