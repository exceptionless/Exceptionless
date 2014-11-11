(function () {
  'use strict';

  angular.module('exceptionless.simple-error', [])
    .factory('simpleErrorService', [function () {
      function getExceptions(exception) {
        var exceptions = [];
        var currentException = exception;
        while (currentException) {
          exceptions.push(currentException);
          currentException = currentException.inner;
        }

        return exceptions;
      }

      var service = {
        getExceptions: getExceptions
      };

      return service;
    }
    ]);
}());
