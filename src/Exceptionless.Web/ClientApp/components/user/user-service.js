(function () {
  'use strict';

  angular.module('exceptionless.user', ['restangular'])
    .factory('userService', function ($cacheFactory, $rootScope, Restangular) {
      var _cache = $cacheFactory('http:user');
      $rootScope.$on('cache:clear', _cache.removeAll);
      $rootScope.$on('cache:clear-user', _cache.removeAll);
      $rootScope.$on('auth:logout', _cache.removeAll);
      $rootScope.$on('UserChanged', _cache.removeAll);

      var _cachedRestangular = Restangular.withConfig(function(RestangularConfigurer) {
        RestangularConfigurer.setDefaultHttpFields({ cache: _cache });
      });

      function addAdminRole(id) {
        return Restangular.one('users', id).one('admin-role').post();
      }

      function getCurrentUser(useCache) {
        if (useCache === undefined || useCache) {
          return _cachedRestangular.one('users', 'me').get();
        }

        return Restangular.one('users', 'me').get();
      }

      function getById(id) {
        return Restangular.one('users', id).get();
      }

      function getByOrganizationId(id, options) {
        return Restangular.one('organizations', id).all('users').getList(options || {});
      }

      function hasAdminRole(user) {
        return hasRole(user, 'global');
      }

      function hasRole(user, role) {
        return !!user && !!user.roles && user.roles.indexOf(role) !== -1;
      }

      function removeAdminRole(id) {
        return Restangular.one('users', id).one('admin-role').remove();
      }

      function removeCurrentUser() {
        return Restangular.one('users', 'me').remove();
      }

      function resendVerificationEmail(id) {
        return Restangular.one('users', id).one('resend-verification-email').get();
      }

      function update(id, project) {
        return Restangular.one('users', id).patch(project);
      }

      function updateEmailAddress(id, email) {
        return Restangular.one('users', id).one('email-address', email).post();
      }

      function verifyEmailAddress(token) {
        return Restangular.one('users', 'verify-email-address').one(token).get();
      }

      var service = {
        addAdminRole: addAdminRole,
        getCurrentUser: getCurrentUser,
        getById: getById,
        getByOrganizationId: getByOrganizationId,
        hasAdminRole: hasAdminRole,
        hasRole: hasRole,
        removeAdminRole: removeAdminRole,
        removeCurrentUser: removeCurrentUser,
        resendVerificationEmail: resendVerificationEmail,
        update: update,
        updateEmailAddress: updateEmailAddress,
        verifyEmailAddress: verifyEmailAddress
      };

      return service;
    });
}());
