(function () {
  'use strict';

  angular.module('exceptionless.rate-limit', [
    'restangular'
  ])
  .run(function(rateLimitService, Restangular) {
    Restangular.addResponseInterceptor(function(data, operation, what, url, response, deferred) {
      rateLimitService.updateFromResponseHeader(response);
      return data;
    });
  });
}());
