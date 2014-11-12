(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .directive('searchFilter', function () {
      return {
        restrict: 'E',
        replace: true,
        scope: true,
        templateUrl: 'components/filter/search-filter-directive.tpl.html',
        controller: ['filterService', function (filterService) {
          var vm = this;

          function hasFilter() {
            return filterService.getFilter() || filterService.getIncludeFixed() || filterService.getIncludeHidden();
          }

          function setFilter(filter) {
            filterService.setFilter(filter);
          }

          function setIncludeFixed(includeFixed) {
            filterService.setIncludeFixed(includeFixed)
          }

          function setIncludeHidden(includeHidden) {
            filterService.setIncludeHidden(includeHidden)
          }

          vm.hasFilter = hasFilter;
          vm.filter = filterService.getFilter();
          vm.includeFixed = filterService.getIncludeFixed();
          vm.includeHidden = filterService.getIncludeHidden();
          vm.isDropDownOpen = false;
          vm.setFilter = setFilter;
          vm.setIncludeFixed = setIncludeFixed;
          vm.setIncludeHidden = setIncludeHidden;
        }],
        controllerAs: 'vm'
      };
    });
}());
