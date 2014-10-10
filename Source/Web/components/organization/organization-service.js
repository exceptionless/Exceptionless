(function () {
    'use strict';

    angular.module('exceptionless.organization', ['restangular'])
        .factory('organizationService', ['Restangular', function (Restangular) {
            function getAll(options) {
                return Restangular.all('organizations').getList(options || {});
            }

            function getById(id) {
                return Restangular.one('organizations', id).get();
            }

            function remove(id) {
                return Restangular.one('organizations', id).remove();
            }

            function update(id, organization){
                return Restangular.one('organizations', id).patch(organization);
            }

            var service = {
                getAll: getAll,
                getById: getById,
                remove: remove,
                update: update
            };
            return service;
        }
    ]);
}());
