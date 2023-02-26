(function () {
  'use strict';

  angular.module('exceptionless.organization', ['restangular'])
    .factory('organizationService', function ($cacheFactory, $rootScope, objectIDService, Restangular) {
      var _cache = $cacheFactory('http:organization');
      $rootScope.$on('cache:clear', _cache.removeAll);
      $rootScope.$on('cache:clear-organization', _cache.removeAll);
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

      function addUser(id, email) {
        return Restangular.one('organizations', id).one('users', email).post();
      }

      function create(name) {
        return Restangular.all('organizations').post({'name': name});
      }

      function changePlan(id, options) {
        return Restangular.one('organizations', id).customPOST(null, 'change-plan', options);
      }

      function getOldestCreationDate(organizations) {
        if (organizations) {
          if (organizations.length > 1) {
            return new Date(organizations.reduce(function (o1, o2) {
                return Math.min(objectIDService.create(o1.id).timestamp, objectIDService.create(o2.id).timestamp);
              }) * 1000);
          }

          if (organizations.length === 1) {
            return objectIDService.getDate(organizations[0].id);
          }
        }

        return new Date(2012, 1, 1);
      }

      function getOldestRetentionStartDate(organizations, maximumRetentionDays) {
        if (!maximumRetentionDays) {
          maximumRetentionDays = moment().diff(new Date(2012, 1, 1), 'days');
        }

        var retentionDays = maximumRetentionDays;
        if (organizations) {
          if (organizations.length > 1) {
            retentionDays = organizations.reduce(function (o1, o2) {
              return Math.max(o1.retention_days > 0 ? o1.retention_days : maximumRetentionDays, o2.retention_days > 0 ? o2.retention_days : maximumRetentionDays);
            });
          } else if (organizations.length === 1) {
            retentionDays = organizations[0].retention_days;
          }
        }

        return retentionDays <= 0 ? new Date(2012, 1, 1) : moment().subtract(retentionDays, 'days').toDate();
      }

      function getOldestPossibleEventDate(organizations, maximumRetentionDays) {
        return moment.max([
          moment(getOldestCreationDate(organizations)).subtract(3, 'days'),
          moment(getOldestRetentionStartDate(organizations, maximumRetentionDays))
        ]).toDate();
      }

      function getAll(options, useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.all('organizations').getList(options || {});
        }

        return Restangular.all('organizations').getList(options || {});
      }

      function getById(id, useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.one('organizations', id).get();
        }

        return Restangular.one('organizations', id).get();
      }

      function getInvoice(id) {
        return Restangular.one('organizations', 'invoice').one(id).get();
      }

      function getInvoices(id, options) {
        return Restangular.one('organizations', id).all('invoices').getList(options || {});
      }

      function getPlans(id) {
        return _cachedRestangular.one('organizations', id).all('plans').getList();
      }

      function isNameAvailable(name) {
        return Restangular.one('organizations', 'check-name').get({ name: encodeURIComponent(name) });
      }

      function remove(id) {
        return Restangular.one('organizations', id).remove();
      }

      function removeUser(id, email) {
        return Restangular.one('organizations', id).one('users', email).remove();
      }

      function update(id, organization) {
        return Restangular.one('organizations', id).patch(organization);
      }

      var service = {
        addUser: addUser,
        create: create,
        changePlan: changePlan,
        getOldestCreationDate: getOldestCreationDate,
        getOldestRetentionStartDate: getOldestRetentionStartDate,
        getOldestPossibleEventDate: getOldestPossibleEventDate,
        getAll: getAll,
        getById: getById,
        getInvoice: getInvoice,
        getInvoices: getInvoices,
        getPlans: getPlans,
        isNameAvailable: isNameAvailable,
        remove: remove,
        removeUser: removeUser,
        update: update
      };
      return service;
    });
}());
