(function () {
  'use strict';

  angular.module('exceptionless.show-on-hover-parent', [])
    .directive('showOnHoverParent', function () {
      return {
        restrict: 'A',
        link : function(scope, element) {
          element.parent().bind('mouseenter', function() {
            element.show();
          });

          element.parent().bind('mouseleave', function() {
            element.hide();
          });

          element.hide();
        }
      };
    });
}());
