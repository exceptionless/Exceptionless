(function () {
  'use strict';

  angular.module('app.organization')
    .controller('organization.Manage', ['$state', '$stateParams', '$window', 'organizationService', 'projectService', 'userService', 'notificationService', 'featureService', 'dialogs', 'dialogService', function ($state, $stateParams, $window, organizationService, projectService, userService, notificationService, featureService, dialogs, dialogService) {
      var organizationId = $stateParams.id;
      var options = {limit: 5};
      var vm = this;

      function addUser() {
        dialogs.create('app/organization/manage/add-user-dialog.tpl.html', 'AddUserDialog as vm').result.then(function (name) {
          function onSuccess(response) {
            vm.users.push(response.data);
          }

          function onFailure() {
            notificationService.error('An error occurred while inviting the user.');
          }

          organizationService.create(name).then(onSuccess, onFailure);
        });
      }

      function get() {
        return organizationService.getById(organizationId)
          .then(function (response) {
            vm.organization = response.data;
          }, function () {
            $state.go('app.dashboard');
            notificationService.error('The organization "' + $stateParams.id + '" could not be found.');
          });
      }

      function getInvoices() {
        return organizationService.getInvoices(organizationId, options)
          .then(function (response) {
            vm.invoices = response.data;
          }, function () {
            notificationService.error('The invoices for this organization could not be loaded.');
          });
      }

      function getUsers() {
        return userService.getByOrganizationId(organizationId, options)
          .then(function (response) {
            vm.users = response.data;
          }, function () {
            notificationService.error('The users for this organization could not be loaded.');
          });
      }

      function hasInvoices() {
        return vm.invoices.length > 0;
      }

      function hasPremiumFeatures() {
        return featureService.hasPremium();
      }

      function removeUser(user) {
        return dialogService.confirmDanger('Are you sure you want to remove this user from your organization?', 'REMOVE USER').then(function () {
          function onSuccess() {
            vm.users.splice(vm.users.indexOf(user), 1);
          }

          function onFailure() {
            notificationService.error('An error occurred while trying to remove the user.');
          }

          return organizationService.removeUser(organizationId, user.id).then(onSuccess, onFailure);
        });
      }

      function resendNotification(user) {
        function onFailure() {
          notificationService.error('An error occurred while trying to resend the notification.');
        }

        return organizationService.addUser(organizationId, user.email_address).catch(onFailure);
      }

      function open(id, event) {
        if (event.ctrlKey || event.which === 2) {
          $window.open('/#/organization/payment/' + id, '_blank');
        } else {
          $state.go('app.organization.payment', {id: id});
        }
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure() {
          notificationService.error('An error occurred while saving the organization.');
        }

        return organizationService.update(organizationId, vm.organization).catch(onFailure);
      }

      vm.addUser = addUser;
      vm.hasInvoices = hasInvoices;
      vm.hasPremiumFeatures = hasPremiumFeatures;
      vm.invoices = [];
      vm.open = open;
      vm.organization = {};
      vm.projects = {
        get: function (options) {
          return projectService.getByOrganizationId(organizationId, options);
        },
        options: {
          limit: 10,
          mode: 'summary'
        }
      };
      vm.removeUser = removeUser;
      vm.resendNotification = resendNotification;
      vm.save = save;
      vm.users = [];

      get().then(getUsers).then(getInvoices);
    }
    ]);
}());
