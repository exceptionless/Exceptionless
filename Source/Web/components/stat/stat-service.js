(function () {
    'use strict';

    angular.module('exceptionless.stat', [
        'exceptionless.filter',
        'restangular'
    ])
    .factory('statService', ['filterService', 'Restangular', function (filterService, Restangular) {
        function getByProjectId(id, options) {
            return Restangular.one('projects', id).one('stats').get(filterService.apply(options));
        }

        function getByStackId(id, options) {
            return Restangular.one('stacks', id).one('stats').get(filterService.apply(options));
        }

        var service = {
            getByProjectId: getByProjectId,
            getByStackId: getByStackId
        };

        return service;
    }]);
}());
