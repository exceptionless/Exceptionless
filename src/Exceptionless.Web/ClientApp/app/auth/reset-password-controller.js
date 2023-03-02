(function () {
  'use strict';

  angular.module('app.auth')
    .controller('auth.ResetPassword', function ($ExceptionlessClient, $state, $stateParams, authService, notificationService, translateService) {
      var vm = this;
      function changePassword(isValid) {
        if (!isValid) {
          return;
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.changePassword.success').setProperty('ResetToken', vm._resetToken).submit();
          notificationService.info(translateService.T('You have successfully changed your password.'));
          return $state.go('auth.login');
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.changePassword.error').setProperty('ResetToken', vm._resetToken).setProperty('response', response).submit();
          var message = translateService.T('An error occurred while trying to change your password.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.changePassword').setProperty('ResetToken', vm._resetToken).submit();
        return authService.resetPassword(vm.data).then(onSuccess, onFailure);
      }

      function cancelResetPassword() {
        function redirectToLoginPage() {
          return $state.go('auth.login');
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.cancelResetPassword.success').setProperty('ResetToken', vm._resetToken).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.cancelResetPassword.error').setProperty('ResetToken', vm._resetToken).setProperty('response', response).submit();
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.cancelResetPassword').setProperty('ResetToken', vm._resetToken).submit();
        return authService.cancelResetPassword(vm._resetToken).then(onSuccess, onFailure).then(redirectToLoginPage, redirectToLoginPage);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.auth.ResetPassword';
        vm._cancelResetToken = $stateParams.cancel === 'true';
        vm._resetToken = $stateParams.token;
        vm.changePassword = changePassword;
        vm.data = {password_reset_token: vm._resetToken};

        if (vm._cancelResetToken) {
          cancelResetPassword();
        }
      };
    });
}());
