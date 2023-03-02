(function () {
  'use strict';

  angular.module('exceptionless.validators')
    .directive('organizationNameAvailableValidator', function($timeout, $q, organizationService) {
      return {
        restrict: 'A',
        require: 'ngModel',
        link: function(scope, element, attrs, ngModel) {
          ngModel.$asyncValidators.unique = function(name) {
            var deferred = $q.defer();

            if (ngModel.$pristine) {
              $timeout(function() {
                deferred.resolve(true);
              }, 0);
            } else {
              organizationService.isNameAvailable(name).then(function(response) {
                if (response.status === 201) {
                  deferred.reject('');
                } else {
                  deferred.resolve(true);
                }
              }, function() {
                deferred.reject('An error occurred while validating the organization name.');
              });
            }

            return deferred.promise;
          };
        }
      };
    });
}());
