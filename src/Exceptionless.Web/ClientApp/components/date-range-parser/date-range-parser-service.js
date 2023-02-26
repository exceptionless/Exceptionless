(function () {
  'use strict';

  angular.module('exceptionless.date-range-parser', [])
    .factory('dateRangeParserService', function () {
      var _rangeRegex = /(\d{4}-\d{2}-\d{2}(?:T(?:\d{2}:\d{2}:\d{2}|\d{2}:\d{2}|\d{2}))?)/g;

      function parse(input) {
        if (!input) {
          return null;
        }

        var matches = [], found;
        while (found = _rangeRegex.exec(input)) {
          matches.push(found[0]);
        }

        if (matches.length === 2) {
          return {
            start: matches[0],
            end: matches[1]
          };
        }

        return null;
      }

      var service = {
        parse: parse
      };

      return service;
    });
}());
