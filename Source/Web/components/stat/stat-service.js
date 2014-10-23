(function () {
    'use strict';

    angular.module('exceptionless.stat', ['restangular'])
        .factory('statService', ['Restangular', function (Restangular) {
            function getByProjectId(id, options) {
                return Restangular.one('projects', id).one('stats').get(options || {});
            }

            function getByStackId(id, options) {
                return Restangular.one('stacks', id).one('stats').get(options || {});
            }

            var service = {
                getByProjectId: getByProjectId,
                getByStackId: getByStackId
            };

            return service;
        }
    ]);
}());
