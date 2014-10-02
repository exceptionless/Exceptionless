(function () {
    'use strict';

    angular.module('exceptionless.exception', [])
        .factory('exceptionService', [function (Restangular) {
            function getExceptions(exception) {
                var exceptions = [];
                var currentException = exception;
                while (currentException) {
                    exceptions.push(currentException);
                    currentException = currentException.inner;
                }

                return exceptions;
            }

            function getTargetMethod(exception) {
                var currentException = exception;
                while (currentException) {
                    if (currentException.target_method && currentException.target_method.is_signature_target) {
                        return currentException.target_method;
                    }
                    currentException = currentException.inner;
                }

                return exception.target_method;
            }

            function getTargetMethodType(exception) {
                var method = getTargetMethod(exception);
                var parts = [];
                if (method.declaring_namespace) {
                    parts.push(method.declaring_namespace);
                }

                if (method.declaring_type) {
                    parts.push(method.declaring_type);
                }

                if (method.name) {
                    parts.push(method.name);
                }

                return parts.join('.').replace('+', '.');
            }

            var service = {
                getExceptions: getExceptions,
                getTargetMethod: getTargetMethod,
                getTargetMethodType: getTargetMethodType
            };
            return service;
        }
    ]);
}());
