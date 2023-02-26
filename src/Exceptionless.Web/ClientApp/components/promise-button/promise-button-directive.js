(function () {
  'use strict';

  angular.module('exceptionless.promise-button', [])
    .directive('promiseButton', function ($compile) {
      return {
        restrict: 'A',
        scope: {
          promiseButton: '&',
          promiseButtonBusyText: '@',
          promiseButtonBusySpinnerClass: '@'
        },
        templateUrl: 'components/promise-button/promise-button-directive.tpl.html',
        transclude: true,
        link: function (scope, element, attrs) {
          element.attr('ng-click', 'start()');
          element.removeAttr('promise-button');
          element.find('[ng-transclude]').removeAttr('ng-transclude');

          scope.running = false;
          scope.start = function() {
            if (scope.running) {
              return;
            }

            var promise = scope.promiseButton();
            if (promise && promise.finally) {
              scope.running = true;
              promise.finally(stop);
            }
          };

          function stop() {
            scope.running = false;
          }

          $compile(element)(scope);
        }
      };
    });
}());
