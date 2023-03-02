(function () {
  'use strict';

  angular.module('exceptionless.relative-time', [])
    .directive('relativeTime', function ($interval) {
      return {
        restrict: 'AE',
        scope: {
          to: '=',
          date: '='
        },
        link: function (scope, element) {
          function setRelativeTimeText() {
            var to = moment(scope.to);
            var date = moment(scope.date);

            var isValid = !!scope.to && to.isValid() && to.year() > 1 && !!scope.date && date.isValid() && date.year() > 1;
            element.text((isValid ? date.to(to, true) : 'never'));
          }

          setRelativeTimeText();
          scope.$watch('to', function(value) {
            setRelativeTimeText();
          });

          scope.$watch('date', function(value) {
            setRelativeTimeText();
          });

          // TODO: implement smarter delay logic. We shouldn't be updating stuff it the interval period is a hour, day, month, year..
          var interval = $interval(setRelativeTimeText, 60 * 1000);
          scope.$on('$destroy', function () {
            $interval.cancel(interval);
          });
        }
      };
    });
}());
