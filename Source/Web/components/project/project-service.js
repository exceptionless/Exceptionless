(function () {
    'use strict';

    angular.module('exceptionless.project', ['restangular'])
        .factory('projectService', ['Restangular', function (Restangular) {
            function getAll(options) {
                return Restangular.all('projects').getList(options || {});
            }

            function getById(id) {
                return Restangular.one('projects', id).get();
            }

            function getByOrganizationId(id, options) {
                return Restangular.all('organizations').one(id, 'projects').get(options || {});
            }

            var service = {
                getAll: getAll,
                getById: getById,
                getByOrganizationId: getByOrganizationId
            };
            return service;
        }
    ]);
}());
