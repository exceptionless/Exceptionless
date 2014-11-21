// Taken from: https://github.com/Karl-Gustav/autoActive.
(function () {
  'use strict';

  angular.module('exceptionless.auto-active', [])
    .directive('autoActive', ['$location', '$timeout', function ($location, $timeout) {
      return {
        restrict: 'A',
        scope: false,
        link: function (scope, element, attr) {
          function setActive() {
            var path = $location.path();
            if (!path) {
              return;
            }

            angular.forEach(element.find('li'), function (li) {
              var anchor = li.querySelector('a');
              if (anchor.href.match('#' + path + '(?=\\?|$)')) {
                angular.element(li).addClass('active');
              } else {
                angular.element(li).removeClass('active');
              }
            });

            if (attr.href) {
              if (attr.href.match('#' + path + '(?=\\?|$)')) {
                element.addClass('active');
              } else {
                element.removeClass('active');
              }
            }
          }

          function setActiveWithTimeout() {
            var timeout = $timeout(setActive, 10);
            scope.$on('$destroy', function() {
              $timeout.cancel(timeout);
            });
          }

          setActiveWithTimeout();
          var unbind = scope.$on('$locationChangeSuccess', setActiveWithTimeout);
          scope.$on('$destroy', function() {
            unbind();
          });
        }
      };
    }]);
}());
