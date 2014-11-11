(function () {
  'use strict';

  angular.module('exceptionless.user', ['restangular'])
    .factory('userService', ['Restangular', function (Restangular) {
      function getByOrganizationId(id, options) {
        return Restangular.one('organizations', id).all('users').getList(options || {});
      }

      var service = {
        getByOrganizationId: getByOrganizationId
      };
      return service;
    }
    ]);
}());
