(function () {
  'use strict';

  angular.module('exceptionless.refresh', [
    'debounce'
  ])
  .directive('refreshOn', function ($parse, $rootScope, debounce) {
    return {
      restrict: 'AE',
      link: function (scope, element, attrs) {
        function runActionOnEvent(name, action, refreshIf) {
          var unbind = $rootScope.$on(name, function (event, data) {
            if (refreshIf && !scope.$eval(refreshIf, { data: data })) {
              return;
            }

            action(scope, { data: data });
          });

          scope.$on('$destroy', function() {
            unbind();
            if (attrs.refreshStopping) {
              var action = $parse(attrs.refreshStopping);
              action(scope);
            }
          });
        }

        if (!attrs.refreshAction) {
          return;
        }

        var action = $parse(attrs.refreshAction);
        if (attrs.refreshDebounce) {
          action = debounce(action, parseInt(attrs.refreshDebounce || 1000), true);
        } else if (attrs.refreshThrottle) {
          action = _.throttle(action, parseInt(attrs.refreshThrottle || 1000));
        }

        if (attrs.refreshOn) {
          angular.forEach(attrs.refreshOn.split(' '), function (name) {
            runActionOnEvent(name, action, attrs.refreshIf);
          });
        }

        if (attrs.refreshAlways) {
          angular.forEach(attrs.refreshAlways.split(' '), function (name) {
            runActionOnEvent(name, action);
          });
        }
      }
    };
  });
}());
