(function () {
  'use strict';

  angular.module('exceptionless.pagination', [])
    .factory('paginationService', function () {
      function getCurrentPageSummary(data, page, limit) {
        page = page ? parseInt(page) : 1;
        limit = limit ? parseInt(limit) : 100;

        var from = ((page - 1) * limit) + 1;
        var to = data && data.length > 0 ? from + data.length - 1 : from;

        return from + '-' + to;
      }

      var service = {
        getCurrentPageSummary: getCurrentPageSummary
      };

      return service;
    });
}());
