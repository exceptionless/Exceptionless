(function () {
  'use strict';

  angular.module('exceptionless.truncate', ['debounce'])
    .directive('truncate', function ($window, $timeout, debounce) {
      return {
        restrict: 'A',
        link: function (scope, element, attrs) {
          var el = angular.element(element);
          var lines = attrs.lines || 1;

          // workaround for single-line truncations so they don't cause height jittering
          var defaultOverflow, defaultWhiteSpace, firstRender;
          if (lines === 1) {
            firstRender = true;
            defaultOverflow = el.css('overflow');
            defaultWhiteSpace = el.css('whitespace');
            if (!defaultOverflow) defaultOverflow = "initial";
            if (!defaultWhiteSpace) defaultWhiteSpace = "initial";
            el.css('overflow', "hidden");
            el.css('white-space', "nowrap");
          }

          // function to provide debounced truncation (useful so window resizing doesn't cause issues)
          var truncate = debounce(function () {
            if (firstRender) {
              firstRender = false;
              el.css('overflow', defaultOverflow);
              el.css('white-space', defaultWhiteSpace);
            }

            el.trunk8({
              lines: lines,
              tooltip: (attrs.overwriteTooltip !== undefined ? attrs.overwriteTooltip === true : true),
            });
          }, 100);

          // execute truncate after a short delay - this is so the browser can calculate the available width of the containing element.
          var timeout = $timeout(truncate, 100);

          // register for resize events as this is the only other time we may need to update
          var window = angular.element($window);
          window.bind('resize', truncate);

          scope.$on('$destroy', function (e) {
            $timeout.cancel(timeout);
            window.unbind('resize', truncate);
          });
        }
      };
    });
}());
