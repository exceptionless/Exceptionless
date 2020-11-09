(function () {
  'use strict';

  angular.module('exceptionless.admin', ['restangular'])
    .factory('adminService', function (Restangular) {
      function changePlan(options) {
        return Restangular.one('admin').customPOST(null, 'change-plan', options);
      }

      var service = {
        changePlan: changePlan
      };

      return service;
    });
}());
