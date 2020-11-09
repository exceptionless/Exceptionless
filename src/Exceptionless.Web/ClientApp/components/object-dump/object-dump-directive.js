(function () {
  'use strict';

  // NOTE: We had a ton of existing handlebars code that would of been time consuming to convert to angular.
  // We will convert this when porting to angular 2.0.
  angular.module('exceptionless.object-dump')
    .directive('objectDump', function (handlebarsService) {
      return {
        restrict: 'E',
        scope: {
          content: '=content',
          templateKey: '=templateKey'
        },
        link: function (scope, element) {
          if (typeof scope.content === 'undefined') {
            return;
          }

          try {
            var content = scope.content;
            var template = handlebarsService.getTemplate(scope.templateKey);

            if (typeof content === 'string' || content instanceof String) {
              try {
                content = JSON.parse(scope.content);
              } catch (ex) {
                template = handlebarsService.getTemplate('pre');
              }
            }

            element.html(template(content));
          } catch (ex) {
            element.text(scope.content);
          }
        }
      };
    });
}());
