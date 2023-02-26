(function () {
  'use strict';

  angular.module('exceptionless.duration', [])
    .directive('duration', function ($interval) {
      return {
        restrict: 'AE',
        scope: {
          value: '=',
          period: '='
        },
        link: function (scope, element) {
          function setDurationText() {
            if (typeof(scope.value) === 'number') {
              var duration = moment.duration(scope.value, scope.period || 'seconds');
              element.text(duration.humanize());
            } else {
              element.text('never');
            }
          }

          setDurationText();
          scope.$watch('value', function(value) {
            setDurationText();
          });

          // TODO: implement smarter delay logic. We shouldn't be updating stuff it the interval period is a hour, day, month, year..
          var interval = $interval(setDurationText, 15 * 1000);
          scope.$on('$destroy', function () {
            $interval.cancel(interval);
          });
        }
      };
    });
}());
