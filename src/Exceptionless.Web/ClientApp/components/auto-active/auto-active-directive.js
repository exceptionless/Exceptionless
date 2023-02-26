// Taken from: https://github.com/Karl-Gustav/autoActive.
(function () {
  'use strict';

  angular.module('exceptionless.auto-active', [])
    .directive('autoActive', function ($location, $timeout) {
      return {
        restrict: 'A',
        scope: false,
        link: function (scope, element, attr) {
          function setActive() {
            function isMatch(href) {
              var pattern = href + '(?=\\?|$)';
              return $location.absUrl().match(pattern);
            }

            if (!$location.path()) {
              return;
            }

            angular.forEach(element.find('li'), function (li) {
              var anchor = li.querySelector('a');
              if (anchor && anchor.href) {
                if (isMatch(anchor.href)) {
                  angular.element(li).addClass('active');
                } else {
                  angular.element(li).removeClass('active');
                }
              }
            });

            if (attr && attr.href) {
              if (isMatch(attr.href)) {
                element.addClass('active');
              } else {
                element.removeClass('active');
              }
            }
          }

          function setActiveWithTimeout() {
            var timeout = $timeout(setActive, 100);
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
    });
}());
