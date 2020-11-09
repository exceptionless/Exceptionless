(function () {
  'use strict';

  angular.module('exceptionless.validators')
    .directive('searchFilterValidator', function($timeout, $q, searchService) {
      return {
        restrict: 'A',
        require: 'ngModel',
        link: function(scope, element, attrs, ngModel) {
          ngModel.$asyncValidators.valid = function(query) {
            var deferred = $q.defer();

            if (ngModel.$pristine) {
              $timeout(function() {
                deferred.resolve(true);
              }, 0);
            } else {
              searchService.validate(query).then(function(response) {
                if (!response.data.is_valid) {
                  deferred.reject(response.data.message);
                }
                deferred.resolve(true);
              }, function() {
                deferred.reject('An error occurred while validating the search filter.');
              });
            }

            return deferred.promise;
          };
        }
      };
    });
}());
