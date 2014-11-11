/*global moment:false*/
(function () {
  'use strict';

  angular.module('exceptionless.time-zone', [])
    .factory('timeZoneService', [function () {
      function getCurrentTimeZoneOffset() {
        return new Date().getTimezoneOffset();
      }

      function getTimeZones() {
        return moment.tz.names();
      }


      var service = {
        getCurrentTimeZoneOffset: getCurrentTimeZoneOffset,
        getTimeZones: getTimeZones
      };

      return service;
    }]);
}());
