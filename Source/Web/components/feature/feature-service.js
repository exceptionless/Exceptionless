(function () {
  'use strict';

  angular.module('exceptionless.feature', [])
    .factory('featureService', [function () {
      var isPremium = true;

      function hasPremium() {
        return isPremium;
      }

      function setPremium(value) {
        isPremium = value === true;
      }

      var service = {
        hasPremium: hasPremium,
        setPremium: setPremium
      };

      return service;
    }
    ]);
}());
