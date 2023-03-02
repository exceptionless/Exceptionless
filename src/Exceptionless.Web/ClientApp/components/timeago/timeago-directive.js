(function () {
  'use strict';

  angular.module('exceptionless.timeago', [])
    .directive('timeago', ['$interval', function ($interval) {
      return {
        restrict: 'AE',
        scope: {
          date: '='
        },
        link: function (scope, element) {
          function setTimeagoText() {
            var date = moment(scope.date);
            element.text((!!scope.date && date.isValid() && date.year() > 1) ? date.fromNow() : 'never');
          }

          setTimeagoText();
          scope.$watch('date', function() {
            setTimeagoText();
          });

          // TODO: implement smarter delay logic. We shouldn't be updating stuff it the interval period is a hour, day, month, year..
          var interval = $interval(setTimeagoText, 15 * 1000);
          scope.$on('$destroy', function () {
            $interval.cancel(interval);
          });
        }
      };
    }]);
}());
