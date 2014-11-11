(function () {
  'use strict';

  angular.module('exceptionless.stat', [
    'exceptionless.filter',
    'restangular'
  ])
    .factory('statService', ['filterService', 'Restangular', function (filterService, Restangular) {
      function get(options) {
        return Restangular.one('stats').get(filterService.apply(options));
      }

      function getByStackId(id, options) {
        return Restangular.one('stacks', id).one('stats').get(filterService.apply(options));
      }

      var service = {
        get: get,
        getByStackId: getByStackId
      };

      return service;
    }]);
}());
