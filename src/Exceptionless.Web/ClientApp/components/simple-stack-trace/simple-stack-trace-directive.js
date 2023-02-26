(function () {
  'use strict';

  angular.module('exceptionless.simple-stack-trace', [
    'ngSanitize',

    'exceptionless',
    'exceptionless.simple-error'
  ])
    .directive('simpleStackTrace', function ($ExceptionlessClient, $sanitize, $sce, simpleErrorService) {
      function buildStackFrames(exceptions, includeHTML) {
        var frames = '';
        for (var index = 0; index < exceptions.length; index++) {
          var stackTrace = exceptions[index].stack_trace;
          if (!!stackTrace) {
            if (includeHTML) {
              frames += '<div class="stack-frame">' + escapeHTML(stackTrace.replace(' ', ''));

              if (index < (exceptions.length - 1)) {
                frames += '<div>--- End of inner exception stack trace ---</div>';
              }

              frames += '</div>';
            } else {
              frames += stackTrace.replace(' ', '');

              if (index < (exceptions.length - 1)) {
                frames += '--- End of inner exception stack trace ---';
              }
            }
          }
        }

        return frames;
      }

      function buildStackTrace(exceptions, includeHTML) {
        return buildStackTraceHeader(exceptions, includeHTML) + buildStackFrames(exceptions.reverse(), includeHTML);
      }

      function buildStackTraceHeader(exceptions, includeHTML) {
        var header = '';
        for (var index = 0; index < exceptions.length; index++) {
          if (includeHTML) {
            header += '<span class="ex-header">';
          }

          if (index > 0) {
            header += ' ---> ';
          }

          var hasType = !!exceptions[index].type;
          if (hasType) {
            if (includeHTML) {
              header += '<span class="ex-type">' + escapeHTML(exceptions[index].type) + '</span>: ';
            } else {
              header += exceptions[index].type + ': ';
            }
          }

          if (exceptions[index].message) {
            if (includeHTML) {
              header += '<span class="ex-message">' + escapeHTML(exceptions[index].message) + '</span>';
            } else {
              header += exceptions[index].message;
            }
          }

          if (hasType) {
            if (includeHTML) {
              header += '</span>';
            } else {
              header += '\r\n';
            }
          }
        }

        return header;
      }

      function escapeHTML(input) {
        if (!input || !input.replace) {
          return input;
        }

        return $sce.trustAsHtml(input
          .replace(/&/g, "&amp;")
          .replace(/</g, "&lt;")
          .replace(/>/g, "&gt;")
          .replace(/"/g, "&quot;")
          .replace(/'/g, "&#039;"));
      }

      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          exception: "=",
          textStackTrace: "=?"
        },
        template: '<pre class="stack-trace"><code ng-bind-html="vm.stackTrace"></code></pre>',
        controller: [function () {
          var vm = this;
          this.$onInit = function $onInit() {
            var errors = simpleErrorService.getExceptions(vm.exception);
            vm.stackTrace = $sce.trustAsHtml(buildStackTrace(errors, true));
            vm.textStackTrace = buildStackTrace(errors, false);
          };
        }],
        controllerAs: 'vm'
      };
    });
}());
