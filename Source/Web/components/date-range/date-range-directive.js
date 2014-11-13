(function () {
  'use strict';

  angular.module('exceptionless.date-picker', [])
    .directive('datePicker', [function() {
      return {
        restrict: 'E',
        replace: true,
        scope: {
          start: '=',
          end: '=',
          ngOptions: '='
        },
        templateUrl: "components/date-range/date-range-directive.tpl.html",
        link: function (scope, element) {
          element.datepicker(scope.ngOptions);
        }
      };
    }]);
}());

