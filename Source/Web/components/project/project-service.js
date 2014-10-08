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

            function getConfig(id) {
                return Restangular.one('projects', id).one('config').get();
            }

            function getNotificationSettings(id, userId) {
                return Restangular.one('projects', id).one('notifications', userId).get();
            }

            function removeConfig(id, key) {
                return Restangular.one('projects', id).one('config', key).remove();
            }

            function removeNotificationSettings(id, userId) {
                return Restangular.one('projects', id).one('notifications', userId).remove();
            }

            function resetData(id) {
                return Restangular.one('projects', id).one('reset-data').get();
            }

            function update(id, project){
                return Restangular.one('projects', id).patch(project);
            }

            function setConfig(id, key, value) {
                return Restangular.all('projects').one(id, 'config').one(key, value).post();
            }

            function setNotificationSettings(id, userId, settings) {
                return Restangular.one('projects', id).one('notifications', userId).post(settings);
            }

            var service = {
                getAll: getAll,
                getById: getById,
                getByOrganizationId: getByOrganizationId,
                getConfig: getConfig,
                getNotificationSettings: getNotificationSettings,
                removeConfig: removeConfig,
                removeNotificationSettings: removeNotificationSettings,
                resetData: resetData,
                setConfig: setConfig,
                setNotificationSettings: setNotificationSettings,
                update: update
            };
            return service;
        }
    ]);
}());
