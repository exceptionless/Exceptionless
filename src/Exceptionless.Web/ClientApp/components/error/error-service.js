(function () {
  'use strict';

  angular.module('exceptionless.error', [])
    .factory('errorService', function () {
      function getExceptions(exception) {
        var exceptions = [];
        var currentException = exception;
        while (currentException) {
          exceptions.push(currentException);
          currentException = currentException.inner;
        }

        return exceptions;
      }

      function getTargetInfo(exception) {
        return exception && exception.data ? exception.data['@target'] : null;
      }

      function getTargetInfoExceptionType(exception) {
        var target = getTargetInfo(exception);
        return target && target.ExceptionType ? target.ExceptionType : null;
      }

      function getTargetInfoMethod(exception) {
        var target = getTargetInfo(exception);
        return target && target.Method ? target.Method : null;
      }

      function getTargetInfoMessage(exception) {
        var target = getTargetInfo(exception);
        return target && target.Message ? target.Message : null;
      }

      return {
        getExceptions: getExceptions,
        getTargetInfoExceptionType: getTargetInfoExceptionType,
        getTargetInfoMethod: getTargetInfoMethod,
        getTargetInfoMessage: getTargetInfoMessage
      };
    });
}());
