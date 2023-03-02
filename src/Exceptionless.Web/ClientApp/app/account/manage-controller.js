(function () {
  'use strict';

  angular.module('app.account')
    .controller('account.Manage', function ($stateParams, $timeout, authService, billingService, dialogService, FACEBOOK_APPID, GOOGLE_APPID, GITHUB_APPID, LIVE_APPID, notificationService, projectService, userService, translateService) {
      var vm = this;
      function activateTab(tabName) {
        switch (tabName) {
          case 'notifications':
            vm.activeTabIndex = 1;
            break;
          case 'password':
            vm.activeTabIndex = 2;
            break;
          case 'external':
            vm.activeTabIndex = 3;
            break;
          default:
            vm.activeTabIndex = 0;
            break;
        }
      }

      function authenticate(provider) {
        function onFailure(response) {
          var message = translateService.T('An error occurred while adding external login.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return authService.authenticate(provider).catch(onFailure);
      }

      function changePassword(isValid) {
        if (!isValid) {
          return;
        }

        function onSuccess() {
          notificationService.info(translateService.T('You have successfully changed your password.'));
          vm.password = {};
          vm.passwordForm.$setUntouched(true);
          vm.passwordForm.$setPristine(true);
        }

        function onFailure(response) {
          var message = translateService.T('An error occurred while trying to change your password.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return authService.changePassword(vm.password).then(onSuccess, onFailure);
      }

      function get(data) {
        if (data && data.type === 'User' && data.deleted && data.id === vm.user.id) {
          notificationService.error(translateService.T('Your user account was deleted. Please create a new account.'));
          return authService.logout(true);
        }

        return getUser().then(getProjects).then(getEmailNotificationSettings);
      }

      function getEmailNotificationSettings() {
        function onSuccess(response) {
          vm.emailNotificationSettings = response.data.plain();
          return vm.emailNotificationSettings;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while loading the notification settings.'));
        }

        vm.emailNotificationSettings = null;
        if (!vm.currentProject.id) {
          return;
        }

        return projectService.getNotificationSettings(vm.currentProject.id, vm.user.id).then(onSuccess, onFailure);
      }

      function getProjects() {
        function onSuccess(response) {
          vm.projects = response.data.plain();

          var currentProjectId = vm.currentProject.id ? vm.currentProject.id : $stateParams.projectId;
          vm.currentProject = vm.projects.filter(function(p) { return p.id === currentProjectId; })[0];
          if (!vm.currentProject) {
            vm.currentProject = vm.projects.length > 0 ? vm.projects[0] : {};
          }

          vm.hasPremiumFeatures = vm.currentProject && vm.currentProject.has_premium_features;
          return vm.projects;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while loading the projects.'));
        }

        return projectService.getAll().then(onSuccess, onFailure);
      }

      function getUser() {
        function onSuccess(response) {
          vm.user = response.data.plain();
          vm.user.o_auth_accounts = vm.user.o_auth_accounts || [];
          vm.hasLocalAccount = vm.user.has_local_account === true;
          return vm.user;
        }

        function onFailure(response) {
          var message = translateService.T('An error occurred while loading your user profile.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return userService.getCurrentUser().then(onSuccess, onFailure);
      }

      function deleteAccount() {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete your account?'), translateService.T('DELETE ACCOUNT')).then(function () {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully removed your user account.'));
            authService.logout();
          }

          function onFailure(response) {
            notificationService.error(translateService.T('An error occurred while trying remove your user account.') + ' ' + response.data.message);
          }

          return userService.removeCurrentUser().then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function hasPremiumEmailNotifications() {
        return vm.user.email_notifications_enabled && vm.emailNotificationSettings && vm.hasPremiumFeatures;
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

      function resendVerificationEmail() {
        function onFailure(response) {
          var message = translateService.T('An error occurred while sending your verification email.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return userService.resendVerificationEmail(vm.user.id).catch(onFailure);
      }

      function saveEmailAddress(isRetrying) {
        function resetCanSaveEmailAddress() {
          vm._canSaveEmailAddress = true;
        }

        function retry(delay) {
          var timeout = $timeout(function() {
            $timeout.cancel(timeout);
            saveEmailAddress(true);
          }, delay || 100);
        }

        if (!vm.emailAddressForm || vm.emailAddressForm.$invalid) {
          resetCanSaveEmailAddress();
          return !isRetrying && retry(1000);
        }

        if (!vm.user.email_address || vm.emailAddressForm.$pending) {
          return retry();
        }

        if (vm._canSaveEmailAddress) {
          vm._canSaveEmailAddress = false;
        } else {
          return;
        }

        function onSuccess(response) {
          vm.user.is_email_address_verified = response.data.is_verified;
        }

        function onFailure(response) {
          var message = translateService.T('An error occurred while saving your email address.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return userService.updateEmailAddress(vm.user.id, vm.user.email_address).then(onSuccess, onFailure).then(resetCanSaveEmailAddress, resetCanSaveEmailAddress);
      }

      function saveEmailNotificationSettings() {
        function onFailure(response) {
          var message = translateService.T('An error occurred while saving your notification settings.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return projectService.setNotificationSettings(vm.currentProject.id, vm.user.id, vm.emailNotificationSettings).catch(onFailure);
      }

      function saveEnableEmailNotification() {
        function onFailure(response) {
          var message = translateService.T('An error occurred while saving your email notification preferences.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return userService.update(vm.user.id, { email_notifications_enabled: vm.user.email_notifications_enabled }).catch(onFailure);
      }

      function saveUser(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure(response) {
          var message = translateService.T('An error occurred while saving your full name.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return userService.update(vm.user.id, vm.user).catch(onFailure);
      }

      function showChangePlanDialog() {
        return billingService.changePlan(vm.currentProject ? vm.currentProject.organization_id : null).catch(function(e){});
      }

      function unlink(account) {
        function onSuccess() {
          vm.user.o_auth_accounts.splice(vm.user.o_auth_accounts.indexOf(account), 1);
        }

        function onFailure(response) {
          var message = translateService.T('An error occurred while removing the external login.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return authService.unlink(account.provider, account.provider_user_id).then(onSuccess, onFailure);
      }

      this.$onInit = function $onInit() {
        vm.activeTabIndex = 0;
        vm.authenticate = authenticate;
        vm._canSaveEmailAddress = true;
        vm.changePassword = changePassword;
        vm.currentProject = {};
        vm.deleteAccount = deleteAccount;
        vm.emailAddressForm = {};
        vm.emailNotificationSettings = null;
        vm.getEmailNotificationSettings = getEmailNotificationSettings;
        vm.hasLocalAccount = false;
        vm.get = get;
        vm.hasPremiumFeatures = false;
        vm.hasPremiumEmailNotifications = hasPremiumEmailNotifications;
        vm.isExternalLoginEnabled = isExternalLoginEnabled;
        vm.password = {};
        vm.passwordForm = {};
        vm.projects = [];
        vm.resendVerificationEmail = resendVerificationEmail;
        vm.saveEmailAddress = saveEmailAddress;
        vm.saveEmailNotificationSettings = saveEmailNotificationSettings;
        vm.saveEnableEmailNotification = saveEnableEmailNotification;
        vm.saveUser = saveUser;
        vm.showChangePlanDialog = showChangePlanDialog;
        vm.unlink = unlink;
        vm.user = {};

        activateTab($stateParams.tab);
        get();
      };
    });
}());
