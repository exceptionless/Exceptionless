(function () {
  'use strict';

  angular.module('exceptionless.stack-trace', [
    'angular-filters',
    'exceptionless.error'
  ])
    .directive('stackTrace', ['errorService', function (errorService) {
      function buildParameters(parameters) {
        var result = '';
        for (var index = 0; index < parameters.length; index++) {
          if (index > 0) {
            result += ', ';
          }

          result += [parameters[index].type_namespace, parameters[index].type].join('.').replace('+', '.');
          if (parameters[index].generic_arguments.length > 0) {
            result += '[' + parameters[index].generic_arguments.join(',') + ']';
          }
        }

        return result;
      }

      function buildStackFrame(frame) {
        if (!frame || !frame.name) {
          return '<null>';
        }

        var result = [frame.declaring_namespace, frame.declaring_type, frame.name].join('.').replace('+', '.');
        if (frame.generic_arguments.length > 0) {
          result += '[' + frame.generic_arguments.join(',') + ']';
        }

        if (frame.parameters.length > 0) {
          result += buildParameters(frame.parameters);
        }

        if (frame.data.ILOffset > 0 || frame.data.NativeOffset > 0) {
          result += ' at offset ' + frame.data.ILOffset || frame.data.NativeOffset;
        }

        if (frame.file_name) {
          result += ' in ' + frame.file_name;
          if (frame.line_number > 0) {
            result += ':line ' + frame.line_number;
          }

          if (frame.column > 0) {
            result += ':col ' + frame.column;
          }
        }

        return result + '\r\n';
      }

      function buildStackFrames(exceptions) {
        var frames = '';
        for (var index = 0; index < exceptions.length; index++) {
          frames += '<div class="stack-frame">';

          for (var frameIndex = 0; frameIndex < exceptions[index].stack_trace.length; frameIndex++) {
            frames += buildStackFrame(exceptions[index].stack_trace[frameIndex]);
          }

          frames += '</div>';

          if (index < (exceptions.length - 1)) {
            frames += '<div>--- End of inner exception stack trace ---</div>';
          }
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
          vm.stackTrace = buildStackTrace(errorService.getExceptions(vm.exception));
        }],
        controllerAs: 'vm'
      };
    }]);
}());
