(function () {
  'use strict';

  angular.module('exceptionless.auth', [
    'restangular',
    'satellizer',
    'ui.router',

    'exceptionless',
    'exceptionless.state'
  ])
  .factory('authService', function ($auth, $ExceptionlessClient, $rootScope, $state, stateService, Restangular) {
    function authenticate(provider, userData) {
      function onSuccess() {
        $rootScope.$emit('auth:login', {});
        // TODO: Figure out a way to submit a session start event here.
      }

      return $auth.authenticate(provider, userData || {}).then(onSuccess);
    }

    function cancelResetPassword(resetToken) {
      return Restangular.one('auth', 'cancel-reset-password').one(resetToken).post();
    }

    function changePassword(changePasswordModel) {
      function onSuccess(response) {
        $auth.setToken(response.data.token);
        return response;
      }

      return Restangular.one('auth', 'change-password').customPOST(changePasswordModel).then(onSuccess);
    }

    function forgotPassword(email) {
      return Restangular.one('auth', 'forgot-password').one(email).get();
    }

    function getToken() {
      return $auth.getToken();
    }

    function isAuthenticated() {
      return $auth.isAuthenticated();
    }

    function isEmailAddressAvailable(email) {
      return Restangular.one('auth', 'check-email-address').one(email).get();
    }

    function login(user) {
      return $auth.login(user).then(function () {
        onLoginSuccess(user);
      });
    }

    function logout(withRedirect, params) {
      function logoutLocally() {
        function redirect() {
          $ExceptionlessClient.submitSessionEnd();
          $ExceptionlessClient.config.setUserIdentity();
          $rootScope.$emit('auth:logout', {});

          if (withRedirect) {
            stateService.save(['auth.']);
            return $state.go('auth.login', params);
          }
        }

        stateService.clear();
        return $auth.logout().then(redirect, redirect);
      }

      return Restangular.one('auth', 'logout').get().then(logoutLocally, logoutLocally);
    }

    function onLoginSuccess(user) {
      $ExceptionlessClient.config.setUserIdentity({ identity: user.email, data: { InviteToken: user.invite_token }});
      $ExceptionlessClient.submitSessionStart();
      $rootScope.$emit('auth:login', {});
    }

    function resetPassword(resetPasswordModel) {
      return Restangular.one('auth', 'reset-password').customPOST(resetPasswordModel);
    }

    function signup(user) {
      function onSuccess(response) {
        $auth.setToken(response.data.token);
        onLoginSuccess(user);
        return response;
      }

      return $auth.signup(user).then(onSuccess);
    }

    function unlink(providerName, providerUserId) {
      function onSuccess(response) {
        $auth.setToken(response.data.token);
        return response;
      }

      return Restangular.one('auth', 'unlink').one(providerName).customPOST({ value: providerUserId }).then(onSuccess);
    }

    var service = {
      authenticate: authenticate,
      cancelResetPassword: cancelResetPassword,
      changePassword: changePassword,
      forgotPassword: forgotPassword,
      getToken: getToken,
      isAuthenticated: isAuthenticated,
      isEmailAddressAvailable: isEmailAddressAvailable,
      login: login,
      logout: logout,
      resetPassword: resetPassword,
      signup: signup,
      unlink: unlink
    };

    return service;
  });
}());
