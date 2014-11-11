(function () {
  'use strict';

  angular.module('exceptionless.organization', ['restangular'])
    .factory('organizationService', ['Restangular', function (Restangular) {
      function addUser(id, email) {
        return Restangular.one('organizations', id).one('users', email).remove();
      }

      function create(name) {
        return Restangular.all('organizations').post({'name': name});
      }

      function getAll(options) {
        return Restangular.all('organizations').getList(options || {});
      }

      function getById(id) {
        return Restangular.one('organizations', id).get();
      }

      function getInvoices(id, options) {
        return Restangular.one('organizations', id).all('invoices').getList(options || {});
      }

      function remove(id) {
        return Restangular.one('organizations', id).remove();
      }

      function removeUser(id, email) {
        return Restangular.one('organizations', id).one('users', email).remove();
      }

      function update(id, organization) {
        return Restangular.one('organizations', id).patch(organization);
      }

      var service = {
        addUser: addUser,
        create: create,
        getAll: getAll,
        getById: getById,
        getInvoices: getInvoices,
        remove: remove,
        removeUser: removeUser,
        update: update
      };
      return service;
    }
    ]);
}());
