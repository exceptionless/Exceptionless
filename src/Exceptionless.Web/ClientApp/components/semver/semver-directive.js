(function () {
  'use strict';

  angular.module('exceptionless.semver', [])
    .directive('semver', function () {
      return {
        restrict: 'A',
        scope: false,
        require: '?ngModel',
        link: function (scope, element, attrs, modelCtrl) {
          modelCtrl.$parsers.push(function (inputValue) {
            var isVersionRegex = /^(\d+)\.(\d+)\.?(\d+)?\.?(\d+)?$/;
            if (!inputValue || !isVersionRegex.test(inputValue)) {
              return inputValue;
            }

            var transformedInput = '';
            var isTwoPartVersion = /^(\d+)\.(\d+)$/;
            var isFourPartVersion = /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/;
            if (isTwoPartVersion.test(inputValue)) {
              transformedInput = inputValue.replace(isTwoPartVersion, '$1.$2.0');
            } else if (isFourPartVersion.test(inputValue)) {
              transformedInput = inputValue.replace(isFourPartVersion, '$1.$2.$3-$4');
            }

            if (transformedInput !== '') {
              modelCtrl.$setViewValue(transformedInput);
              modelCtrl.$render();
            }

            return inputValue;
          });
        }
      };
    });
}());
