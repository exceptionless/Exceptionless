(function () {
  'use strict';

  angular.module('app.auth')
    .controller('auth.Login', function ($ExceptionlessClient, $state, $stateParams, translateService, authService, BASE_URL, FACEBOOK_APPID, GOOGLE_APPID, GITHUB_APPID, LIVE_APPID, ENABLE_ACCOUNT_CREATION, notificationService, projectService, stateService) {
      var vm = this;

      function getMessage(response) {
        var message = translateService.T("Loggin_Failed_Message");
        if (response.data && response.data.message) {
          message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
        } else if (response.status < 0) {
          return translateService.T('Unable_to_connect_to') + ' ' + BASE_URL + '.';
        }

        return message;
      }

      function authenticate(provider) {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.authenticate').setProperty('InviteToken', vm.token).addTags(provider).submit();
          return redirectOnSignup();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.authenticate.error').setProperty('InviteToken', vm.token).setProperty('response', response).addTags(provider).submit();
          notificationService.error(getMessage(response));
        }

        return authService.authenticate(provider, { InviteToken: vm.token }).then(onSuccess, onFailure);
      }

      function isExternalLoginEnabled(provider) {
        if (!provider) {
          return !!FACEBOOK_APPID || !!GITHUB_APPID || !!GOOGLE_APPID || !!LIVE_APPID;
        }

        switch (provider) {
          case 'facebook':
            return !!FACEBOOK_APPID;
          case 'github':
            return !!GITHUB_APPID;
          case 'google':
            return !!GOOGLE_APPID;
          case 'live':
            return !!LIVE_APPID;
          default:
            return false;
        }
      }

      function login(isValid) {
        if (!isValid) {
          return;
        }

        function onSuccess() {
          $ExceptionlessClient.submitFeatureUsage(vm._source + '.login');
          return redirectOnSignup();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.login.error').setUserIdentity(vm.user.email).submit();
          notificationService.error(getMessage(response));
        }

        return authService.login(vm.user).then(onSuccess, onFailure);
      }

      function redirectOnSignup() {
        function onSuccess(response) {
          if (response.data && response.data.length > 0) {
            return stateService.restore();
          }

          stateService.clear();
          return $state.go('app.project.add');
        }

        function onFailure() {
          return stateService.restore('app.project.add');
        }

        return projectService.getAll().then(onSuccess, onFailure);
      }

      if (authService.isAuthenticated()) {
        authService.logout();
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.auth.Login';
        vm.authenticate = authenticate;
        vm.isExternalLoginEnabled = isExternalLoginEnabled;
        vm.enableAccountCreation = !!ENABLE_ACCOUNT_CREATION;
        vm.login = login;
        vm.loginForm = {};
        vm.token = $stateParams.token;
        vm.user = {invite_token: vm.token};
      };
    });
}());
