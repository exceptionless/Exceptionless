(function () {
  'use strict';

  angular.module('exceptionless.ui-scroll', [])
    .directive('uiScroll', function ($location, $anchorScroll) {
      return {
        restrict: 'AC',
        link: function (scope, el, attr) {
          el.on('click', function (e) {
            $location.hash(attr.uiScroll);
            $anchorScroll();
          });
        }
      };
    });
}());
