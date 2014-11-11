(function () {
  'use strict';

  angular.module('exceptionless.web-hook')
    .factory('webHookService', ['Restangular', function (Restangular) {
      function create(webHook) {
        return Restangular.all('webhooks').post(webHook);
      }

      function getAll(options) {
        return Restangular.all('webhooks').getList(options || {});
      }

      function getById(id) {
        return Restangular.one('webhooks', id).get();
      }

      function getByOrganizationId(id, options) {
        return Restangular.one('organizations', id).all('webhooks').getList(options || {});
      }

      function getByProjectId(id, options) {
        return Restangular.one('projects', id).all('webhooks').getList(options || {});
      }


      var service = {
        create: create,
        getAll: getAll,
        getById: getById,
        getByOrganizationId: getByOrganizationId,
        getByProjectId: getByProjectId
      };
      return service;
    }
    ]);
}());
