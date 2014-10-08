(function () {
    'use strict';

    angular.module('exceptionless.token', ['restangular'])
        .factory('tokenService', ['Restangular', function (Restangular) {
            function add(token) {
                return Restangular.all('tokens').post(token);
            }

            function getById(id) {
                return Restangular.one('tokens', id).get();
            }

            function getByOrganizationId(id, options) {
                return Restangular.one('prganizations', id).one('tokens').get(options || {});
            }

            function getByProjectId(id, options) {
                return Restangular.one('projects', id).one('tokens').get(options || {});
            }

            function getProjectDefault(id) {
                return Restangular.one('projects', id).one('tokens', 'default').get();
            }

            function remove() {
                return Restangular.one('tokens', id).remove();
            }

            var service = {
                add: add,
                getById: getById,
                getByOrganizationId: getByOrganizationId,
                getByProjectId: getByProjectId,
                getProjectDefault: getProjectDefault,
                remove: remove
            };
            return service;
        }
    ]);
}());
