(function () {
  'use strict';

  angular.module('exceptionless.status', [
    'restangular'
  ])
  .factory('statusService', function (Restangular, BASE_URL) {
    function about() {
      return Restangular.oneUrl('HealthChecks', BASE_URL + '/api/v2/about').get();
    }

    function get() {
      return Restangular.oneUrl('HealthChecks', BASE_URL + '/health').get();
    }

    var service = {
      about: about,
      get: get
    };

    return service;
  });
}());
