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
          function setFilter(filter) {
            filterService.setFilter(filter);
          }

          var vm = this;
          vm.filter = null;
          vm.setFilter = setFilter;
        }],
        controllerAs: 'vm'
      };
    });
}());
