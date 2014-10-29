(function () {
    'use strict';

    angular.module('exceptionless.event', ['restangular'])
        .factory('eventService', ['Restangular', function (Restangular) {
            function getAll(options) {
                return Restangular.all('events').getList(options || {});
            }

            function getById(id) {
                return Restangular.one('events', id).get();
            }

            function getByStackId(id, options) {
                return Restangular.one('stacks', id).all('events').getList(options || {});
            }

            function markCritical(id) {
                return Restangular.one('events', id).one('mark-critical').post();
            }

            function markNotCritical(id) {
                return Restangular.one('events', id).one('mark-critical').remove();
            }

            function remove(id) {
                return Restangular.one('events', id).remove();
            }

            var service = {
                getAll: getAll,
                getById: getById,
                getByStackId: getByStackId,
                markCritical: markCritical,
                markNotCritical: markNotCritical,
                remove: remove
            };
            return service;
        }
    ]);
}());
