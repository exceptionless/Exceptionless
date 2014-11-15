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
            if (!scope.date) {
              element.text('Loading..');
            } else {
              element.text(moment(scope.date).fromNow());
            }
          }

          setTimeagoText();
          scope.$watch('date', function(value) {
            setTimeagoText();
          });

          // TODO: implement smarter delay logic. We shouldn't be updating stuff it the interval period is a hour, day, month, year..
          var interval = $interval(setTimeagoText, 60 * 1000);
          scope.$on('$destroy', function () {
            $interval.cancel(interval);
          });
        }
      };
    }]);
}());
