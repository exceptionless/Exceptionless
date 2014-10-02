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
                return Restangular.all('stacks').one(id, 'events').get(options || {});
            }

            var service = {
                getAll: getAll,
                getById: getById,
                getByStackId: getByStackId
            };
            return service;
        }
    ]);
}());