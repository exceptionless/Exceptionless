(function () {
  'use strict';

  angular.module('app.auth')
    .controller('auth.Logout', function ($state, authService) {
      this.$onInit = function $onInit() {
        if (authService.isAuthenticated()) {
          authService.logout();
        }

        $state.go('auth.login');
      };
    });
}());
