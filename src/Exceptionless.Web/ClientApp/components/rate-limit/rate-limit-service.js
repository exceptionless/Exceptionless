(function () {
  'use strict';

  angular.module('exceptionless.rate-limit')
  .factory('rateLimitService', function ($rootScope) {
    var _rateLimit = -1;
    var _rateLimitExceeded = false;
    var _rateLimitRemaining = -1;

    function rateLimitExceeded() {
      return _rateLimitExceeded;
    }

    function updateFromResponseHeader(response) {
      var limit = parseInt(response.headers('X-RateLimit-Limit'));
      _rateLimit = !isNaN(limit) ? limit : -1;

      var limitRemaining = parseInt(response.headers('X-RateLimit-Remaining'));
      _rateLimitRemaining = !isNaN(limitRemaining) ? limitRemaining : -1;

      _rateLimitExceeded = _rateLimit > 0 ? _rateLimitRemaining <= 0 : false;
    }

    var service = {
      rateLimitExceeded: rateLimitExceeded,
      updateFromResponseHeader: updateFromResponseHeader
    };

    return service;
  });
}());
