(function () {
  'use strict';

  angular.module('exceptionless.validators')
    .directive('semanticVersionValidator', function() {
      return {
        restrict: 'A',
        require: 'ngModel',
        link: function(scope, element, attrs, ngModel) {
          ngModel.$validators.semver = function (modelValue, viewValue) {
            if (typeof viewValue !== 'string') {
              return true;
            }

            var version = viewValue.trim();
            if (version.length === 0) {
              return true;
            }

            if (version.length > 256) {
              return false;
            }

            var regex = new RegExp('^v?(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][a-zA-Z0-9-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][a-zA-Z0-9-]*))*))?(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$');
            return regex.test(version);
          };
        }
      };
    });
}());
