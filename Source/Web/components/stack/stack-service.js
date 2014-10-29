(function () {
    'use strict';

    angular.module('exceptionless.stack', ['restangular'])
        .factory('stackService', ['Restangular', function (Restangular) {
            function addLink(id, url) {
                return Restangular.one('stacks', id).one('add-link').customPOST(url);
            }

            function disableNotifications(id) {
                return Restangular.one('stacks', id).one('notifications').remove();
            }

            function enableNotifications(id) {
                return Restangular.one('stacks', id).one('notifications').post();
            }

            function getAll(options) {
                return Restangular.all('stacks').getList(options || {});
            }

            function getById(id) {
                return Restangular.one('stacks', id).get();
            }

            function getFrequentByProjectId(id, options) {
                return Restangular.one('projects', id).one('stacks').all('frequent').getList(options || {});
            }

            function getNewByProjectId(id, options) {
                return Restangular.one('projects', id).one('stacks').all('new').getList(options || {});
            }

            function markCritical(id) {
                return Restangular.one('stacks', id).one('mark-critical').post();
            }

            function markNotCritical(id) {
                return Restangular.one('stacks', id).one('mark-critical').remove();
            }

            function markFixed(id) {
                return Restangular.one('stacks', id).one('mark-fixed').post();
            }

            function markNotFixed(id) {
                return Restangular.one('stacks', id).one('mark-fixed').remove();
            }

            function markHidden(id) {
                return Restangular.one('stacks', id).one('mark-hidden').post();
            }

            function markNotHidden(id) {
                return Restangular.one('stacks', id).one('mark-hidden').remove();
            }

            function promote(id) {
                return Restangular.one('stacks', id).one('promote').post();
            }

            function remove(id) {
                return Restangular.one('stacks', id).remove();
            }

            function removeLink(id, url) {
                return Restangular.one('stacks', id).one('remove-link').customPOST(url);
            }

            var service = {
                addLink: addLink,
                disableNotifications: disableNotifications,
                enableNotifications: enableNotifications,
                getAll: getAll,
                getById: getById,
                getFrequentByProjectId: getFrequentByProjectId,
                getNewByProjectId: getNewByProjectId,
                markCritical: markCritical,
                markNotCritical: markNotCritical,
                markFixed: markFixed,
                markNotFixed: markNotFixed,
                markHidden: markHidden,
                markNotHidden: markNotHidden,
                promote: promote,
                remove: remove,
                removeLink: removeLink
            };

            return service;
        }
    ]);
}());
