(function () {
  'use strict';

  // NOTE: We had a ton of existing handlebars code that would of been time consuming to convert to angular.
  // We will convert this when porting to angular 2.0.
  angular.module('exceptionless.object-dump')
    .directive('objectDump', ['handlebarsService', function (handlebarsService) {
      return {
        restrict: 'E',
        scope: {
          content: '=content',
          templateKey: '=templateKey'
        },
        link: function (scope, element) {
          if (!scope.content) {
            return;
          }

          try {
            var content = scope.content;
            if (typeof content === 'string' || content instanceof String) {
              content = JSON.parse(scope.content);
            }

            var template = handlebarsService.getTemplate(scope.templateKey);
            element.html(template(content));
          } catch (ex) {
            element.text(scope.content);
          }
        }
      };
    }]);
}());
