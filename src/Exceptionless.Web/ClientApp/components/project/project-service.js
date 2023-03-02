(function () {
  'use strict';

  angular.module('exceptionless.project')
    .factory('projectService', function ($auth, $cacheFactory, $rootScope, Restangular) {
      var _cache = $cacheFactory('http:project');
      $rootScope.$on('cache:clear', _cache.removeAll);
      $rootScope.$on('cache:clear-project', _cache.removeAll);
      $rootScope.$on('auth:logout', _cache.removeAll);
      $rootScope.$on('OrganizationChanged', _cache.removeAll);
      $rootScope.$on('ProjectChanged', _cache.removeAll);

      $rootScope.$on('StackChanged', function($event, data) {
        if (data.added) {
          _cache.removeAll();
        }
      });

      var _cachedRestangular = Restangular.withConfig(function(RestangularConfigurer) {
        RestangularConfigurer.setDefaultHttpFields({ cache: _cache });
      });

      function addSlack(id) {
        function onSuccess(response) {
          return Restangular.one('projects', id).post('slack', null, { code: response.code });
        }

        return $auth.link('slack').then(onSuccess);
      }

      function create(organizationId, name) {
        return Restangular.all('projects').post({'organization_id': organizationId, 'name': name, delete_bot_data_enabled: true  });
      }

      function demoteTab(id, name) {
        return Restangular.one('projects', id).one('promotedtabs').remove({ name: name });
      }

      function getAll(options, useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.all('projects').getList(angular.extend({}, { limit: 1000 }, options));
        }

        return Restangular.all('projects').getList(angular.extend({}, { limit: 1000 }, options));
      }

      function getById(id, useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.one('projects', id).get();
        }

        return Restangular.one('projects', id).get();
      }

      function getByOrganizationId(id, options, useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.one('organizations', id).all('projects').getList(options || {});
        }

        return Restangular.one('organizations', id).all('projects').getList(options || {});
      }

      function getConfig(id) {
        return _cachedRestangular.one('projects', id).one('config').get();
      }

      function getNotificationSettings(id, userId) {
        return _cachedRestangular.one('users', userId).one('projects', id).one('notifications').get();
      }

      function getIntegrationNotificationSettings(id, integration) {
        return _cachedRestangular.one('projects', id).one(integration, 'notifications').get();
      }

      function isNameAvailable(organizationId, name) {
        return Restangular.one('organizations', organizationId).one('projects', 'check-name').get({ name: encodeURIComponent(name) });
      }

      function promoteTab(id, name) {
        return Restangular.one('projects', id).post('promotedtabs', null, { name: name });
      }

      function remove(id) {
        return Restangular.one('projects', id).remove();
      }

      function removeConfig(id, key) {
        return Restangular.one('projects', id).one('config').remove({ key: key });
      }

      function removeData(id, key) {
        return Restangular.one('projects', id).one('data').remove({ key: key });
      }

      function removeSlack(id) {
        return Restangular.one('projects', id).one('slack').remove();
      }

      function removeNotificationSettings(id, userId) {
        return Restangular.one('users', userId).one('projects', id).one('notifications').remove();
      }

      function resetData(id) {
        return Restangular.one('projects', id).one('reset-data').get();
      }

      function update(id, project) {
        return Restangular.one('projects', id).patch(project);
      }

      function setConfig(id, key, value) {
        return Restangular.one('projects', id).post('config', { value: value }, { key: key }, {});
      }

      function setData(id, key, value) {
        return Restangular.one('projects', id).post('data', { value: value }, { key: key }, {});
      }

      function setNotificationSettings(id, userId, settings) {
        return Restangular.one('users', userId).one('projects', id).post('notifications', settings);
      }

      function setIntegrationNotificationSettings(id, integration, settings) {
        return Restangular.one('projects', id).one(integration).post('notifications', settings);
      }

      var service = {
        addSlack: addSlack,
        create: create,
        demoteTab: demoteTab,
        getAll: getAll,
        getById: getById,
        getByOrganizationId: getByOrganizationId,
        getConfig: getConfig,
        getNotificationSettings: getNotificationSettings,
        getIntegrationNotificationSettings: getIntegrationNotificationSettings,
        isNameAvailable: isNameAvailable,
        promoteTab: promoteTab,
        remove: remove,
        removeConfig: removeConfig,
        removeData: removeData,
        removeNotificationSettings: removeNotificationSettings,
        removeSlack: removeSlack,
        resetData: resetData,
        setConfig: setConfig,
        setData: setData,
        setNotificationSettings: setNotificationSettings,
        setIntegrationNotificationSettings: setIntegrationNotificationSettings,
        update: update
      };
      return service;
    });
}());
