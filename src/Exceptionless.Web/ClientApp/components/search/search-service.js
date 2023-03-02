(function () {
  'use strict';

  angular.module('exceptionless.search', ['restangular'])
    .factory('searchService', function ($cacheFactory, $timeout, $q, Restangular) {
      var _cache = $cacheFactory('http:search');

      var _cachedRestangular = Restangular.withConfig(function(RestangularConfigurer) {
        RestangularConfigurer.setDefaultHttpFields({ cache: _cache });
      });

      function validate(query) {
        if (!query || (query.trim && query.trim() === '*')) {
          var deferred = $q.defer();
          $timeout(function() {
            deferred.resolve({
              data: {
                is_valid: true,
                uses_premium_features: false
              }
            });
          }, 0);

          return deferred.promise;
        }

        return _cachedRestangular.one('search', 'validate').get({ query: query });
      }

      var service = {
        validate: validate
      };

      return service;
    });
}());
