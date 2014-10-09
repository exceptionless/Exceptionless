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

            var service = {
                getAll: getAll,
                getById: getById,
                getByStackId: getByStackId
            };
            return service;
        }
    ]);
}());
