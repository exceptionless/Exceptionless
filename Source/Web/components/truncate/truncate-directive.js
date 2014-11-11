(function () {
  'use strict';

  angular.module('exceptionless.truncate', ['debounce'])
    .directive('truncate', ['$window', '$timeout', 'debounce', function ($window, $timeout, debounce) {
      return {
        restrict: 'A',
        link: function (scope, element, attrs) {
          var truncate = debounce(function () {
            angular.element(element).trunk8({lines: attrs.lines || 1});
          }, 150);

          // TODO: Fix this bug: http://branchandbound.net/blog/web/2013/08/some-angularjs-pitfalls/
          var timeout = $timeout(truncate, 1);

          var window = angular.element($window);
          window.bind('resize', truncate);

          scope.$on('$destroy', function (e) {
            $timeout.cancel(timeout);
            window.unbind('resize', truncate);
          });
        }
      };
    }]);
}());
