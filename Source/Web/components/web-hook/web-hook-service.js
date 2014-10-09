(function () {
    'use strict';

    angular.module('exceptionless.web-hook')
        .factory('webHookService', ['Restangular', function (Restangular) {
            function getAll(options) {
                return Restangular.all('webhooks').getList(options || {});
            }

            function getById(id) {
                return Restangular.one('webhooks', id).get();
            }

            function getByOrganizationId(id, options) {
                return Restangular.all('organizations').one(id, 'webhooks').get(options || {});
            }

            function getByProjectId(id, options) {
                return Restangular.all('projects').one(id, 'webhooks').get(options || {});
            }

            var service = {
                getAll: getAll,
                getById: getById,
                getByOrganizationId: getByOrganizationId,
                getByProjectId: getByProjectId
            };
            return service;
        }
    ]);
}());
