(function () {
  'use strict';

  angular.module('exceptionless.loading-bar', [
    'cfp.loadingBarInterceptor'
  ])
  .directive('loadingBar', ['$rootScope', function ($rootScope) {
    return {
      bindToController: true,
      restrict: 'A',
      link: function (scope, element, attrs) {
        var _hideOnCompleted = false;
        $rootScope.$on('cfpLoadingBar:completed', function() {
          if (_hideOnCompleted) {
            _hideOnCompleted = false;
            element.addClass('loading-bar-disabled');
          }
        });

        $rootScope.$on('$stateChangeStart', function() {
          element.removeClass('loading-bar-disabled');
          _hideOnCompleted = true;
        });
      }
    };
  }]);
}());
