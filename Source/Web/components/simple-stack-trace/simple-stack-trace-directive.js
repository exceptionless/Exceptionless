(function () {
  'use strict';

  angular.module('exceptionless.simple-stack-trace', [
    'exceptionless.simple-error'
  ])
    .directive('simpleStackTrace', ['simpleErrorService', function (simpleErrorService) {
      function buildStackFrames(exceptions) {
        var frames = '';
        for (var index = 0; index < exceptions.length; index++) {
          frames += '<div class="stack-frame">' + exceptions[index].stack_trace.replace(' ', '');

          if (index < (exceptions.length - 1)) {
            frames += '<div>--- End of inner exception stack trace ---</div>';
          }

          frames += '</div>';
        }

        return frames;
      }

      function buildStackTrace(exceptions) {
        return buildStackTraceHeader(exceptions) + buildStackFrames(exceptions.reverse());
      }

      function buildStackTraceHeader(exceptions) {
        var header = '';
        for (var index = 0; index < exceptions.length; index++) {
          header += '<span class="ex-header">';

          if (index > 0) {
            header += ' ---> ';
          }

          header += '<span class="ex-type">' + exceptions[index].type + '</span>';
          if (exceptions[index].message) {
            header += '<span class="ex-message">: ' + exceptions[index].message + '</span>';
          }

          header += '</span>';
        }

        return header;
      }

      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          exception: "="
        },
        template: '<pre class="stack-trace" ng-bind-html="vm.stackTrace"></pre>',
        controller: [function () {
          var vm = this;
          vm.stackTrace = buildStackTrace(simpleErrorService.getExceptions(vm.exception));
        }],
        controllerAs: 'vm'
      };
    }]);
}());
