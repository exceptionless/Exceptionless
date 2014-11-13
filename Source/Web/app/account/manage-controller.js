(function () {
  'use strict';

  angular.module('app.account')
    .controller('account.Manage', ['projectService', 'notificationService', 'featureService', function (projectService, notificationService, featureService) {
      var vm = this;

      function changePassword(isValid) {

      }

      function getProjects() {
        return projectService.getAll()
          .then(function (response) {
            vm.projects = response.data;
          }, function () {
            notificationService.error('An error occurred while loading the projects.');
          });
      }

      function hasPremiumFeatures() {
        return featureService.hasPremium();
      }

      function hasProjects() {
        return vm.projects.length > 0;
      }

      function isVerified() {
        return false;
      }

      function resendVerificationEmail() {

      }

      function saveProfile(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure() {
          notificationService.error('An error occurred while saving the project.');
        }

        return projectService.update(vm.currentProject.id, vm.currentProject).catch(onFailure);
      }

      function saveNotificationSettings() {

      }

      vm.changePassword = changePassword;
      vm.currentProject = {};
      vm.hasPremiumFeatures = hasPremiumFeatures;
      vm.hasProjects = hasProjects;
      vm.isVerified = isVerified;
      vm.password = {};
      vm.profile = {};
      vm.projects = [];
      vm.resendVerificationEmail = resendVerificationEmail;
      vm.saveProfile = saveProfile;
      vm.saveNotificationSettings = saveNotificationSettings;

      getProjects();
    }
    ]);
}());
