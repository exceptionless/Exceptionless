(function () {
  'use strict';

  angular.module('app.auth')
    .controller('auth.ForgotPassword', function ($ExceptionlessClient, $state, translateService, authService, notificationService) {
      var vm = this;
      function resetPassword(isValid) {
        if (!isValid) {
          return;
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.resetPassword.success').setUserIdentity(vm.email).submit();
          notificationService.info(translateService.T("ResetPassword_Success_Message"));
          return $state.go('auth.login');
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.resetPassword.error').setUserIdentity(vm.email).setProperty('response', response).submit();
          var message = translateService.T("ResetPassword_Failed_Message");
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.resetPassword').setUserIdentity(vm.email).submit();
        return authService.forgotPassword(vm.email).then(onSuccess, onFailure);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.auth.ForgotPassword';
        vm.email = null;
        vm.resetPassword = resetPassword;
      };
    });
}());
