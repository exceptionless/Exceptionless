// Taken from: https://gist.github.com/kirkstrobeck/599664399dbc23968741.
(function () {
  'use strict';

  angular.module('exceptionless.autofocus', [])
    .directive('autofocus', function ($timeout) {
      return {
        restrict: 'A',
        scope: false,
        link: function (scope, element, attr) {
          var timeout = $timeout(function () {
            element[0].focus();
          });

          scope.$on('$destroy', function() {
            $timeout.cancel(timeout);
          });
        }
      };
    });
}());
