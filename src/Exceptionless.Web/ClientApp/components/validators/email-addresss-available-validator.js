(function () {
  'use strict';

  angular.module('exceptionless.validators')
    .directive('emailAddressAvailableValidator', function($timeout, $q, authService) {
      return {
        restrict: 'A',
        require: 'ngModel',
        link: function(scope, element, attrs, ngModel) {
          ngModel.$asyncValidators.unique = function(emailAddress) {
            var deferred = $q.defer();

            if (ngModel.$pristine) {
              $timeout(function() {
                deferred.resolve(true);
              }, 0);
            } else {
              authService.isEmailAddressAvailable(emailAddress).then(function(response) {
                if (response.status === 201) {
                  deferred.reject('');
                } else {
                  deferred.resolve(true);
                }
              }, function(response) {
                if (response && response.status < 0) {
                  deferred.resolve(true);
                } else {
                  deferred.reject('An error occurred while validating the email address.');
                }
              });
            }

            return deferred.promise;
          };
        }
      };
    });
}());
