(function () {
  'use strict';

  angular.module('exceptionless.summary', ['exceptionless.truncate'])
    .directive('summary', [function () {
      return {
        restrict: 'E',
        scope: {
          source: '=',
          showType: '='
        },
        template: '<ng-include src="templateUrl" />',
        link: function (scope, element, attrs) {
          scope.templateUrl = 'components/summary/templates/' + scope.source.template_key + '.tpl.html';
        }
      };
    }]);
}());
