(function () {
  'use strict';

  angular.module('exceptionless.project', ['restangular'])
    .factory('projectService', ['Restangular', function (Restangular) {
      function create(organizationId, name) {
        return Restangular.all('projects').post({'organization_id': organizationId, 'name': name});
      }

      function demoteTab(id, name) {
        return Restangular.one('projects', id).one('promotedtabs', name).remove();
      }

      function getAll(options) {
        return Restangular.all('projects').getList(options || {});
      }

      function getById(id) {
        function onSuccess(response) {
          var projects = response.data.filter(function(project) { return project.id === id; });
          return projects.length > 0 ? projects[0] : null;
        }

        // NOTE: getById calls getAll because this will always be cached throughout the site..
        return getAll().then(onSuccess);
      }

      function getByOrganizationId(id, options) {
        return Restangular.one('organizations', id).all('projects').getList(options || {});
      }

      function getConfig(id) {
        return Restangular.one('projects', id).one('config').get();
      }

      function getNotificationSettings(id, userId) {
        return Restangular.one('projects', id).one('notifications', userId).get();
      }

      function promoteTab(id, name) {
        return Restangular.one('projects', id).one('promotedtabs', name).post();
      }

      function remove(id) {
        return Restangular.one('projects', id).remove();
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

      function update(id, project) {
        return Restangular.one('projects', id).patch(project);
      }

      function setConfig(id, key, value) {
        return Restangular.one('projects', id).one('config', key).customPOST(value);
      }

      function setNotificationSettings(id, userId, settings) {
        return Restangular.one('projects', id).one('notifications', userId).post(settings);
      }

      var service = {
        create: create,
        demoteTab: demoteTab,
        getAll: getAll,
        getById: getById,
        getByOrganizationId: getByOrganizationId,
        getConfig: getConfig,
        getNotificationSettings: getNotificationSettings,
        promoteTab: promoteTab,
        remove: remove,
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
