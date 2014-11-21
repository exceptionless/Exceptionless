(function () {
  'use strict';

  angular.module('app.organization')
    .controller('organization.List', ['$rootScope', '$scope', '$window', '$state', 'dialogs', 'dialogService', 'linkService', 'notificationService', 'organizationService', function ($rootScope, $scope, $window, $state, dialogs, dialogService, linkService, notificationService, organizationService) {
      var settings = {limit: 10, mode: 'summary'};
      var vm = this;

      function add() {
        dialogs.create('app/organization/list/add-organization-dialog.tpl.html', 'AddOrganizationDialog as vm').result.then(function (name) {
          function onSuccess(response) {
            vm.organizations.push(response.data);
          }

          function onFailure() {
            notificationService.error('An error occurred while creating the organization.');
          }

          organizationService.create(name).then(onSuccess, onFailure);
        });
      }

      function get(options) {
        organizationService.getAll(options || settings).then(function (response) {
          vm.organizations = response.data.plain();

          var links = linkService.getLinksQueryParameters(response.headers('link'));
          vm.previous = links['previous'];
          vm.next = links['next'];
        });
      }

      function leave(organization) {
        return dialogService.confirmDanger('Are you sure you want to leave this organization?', 'LEAVE ORGANIZATION').then(function () {
          function onSuccess() {
            vm.organizations.splice(vm.organizations.indexOf(organization), 1);
          }

          function onFailure(response) {
            // TODO: Show upgrade dialog.
            var message = 'An error occurred while trying to leave the organization.';
            if (response.status === 400) {
              message += ' Message: ' + response.data.message;
            }

            notificationService.error(message);
          }

          // TODO: Inject the current user.
          return organizationService.removeUser(organization.id, 'test@exceptionless.com').then(onSuccess, onFailure);
        });
      }

      function open(id, event) {
        if (event.ctrlKey || event.which === 2) {
          $window.open('/#/organization/' + id + '/manage/', '_blank');
        } else {
          $state.go('app.organization.manage', { id: id });
        }
      }

      function nextPage() {
        get(vm.next);
      }

      function previousPage() {
        get(vm.previous);
      }

      function remove(organization) {
        return dialogService.confirmDanger('Are you sure you want to remove the organization?', 'REMOVE ORGANIZATION').then(function () {
          function onSuccess() {
            vm.organizations.splice(vm.organizations.indexOf(organization), 1);
          }

          function onFailure(response) {
            var message = 'An error occurred while trying to remove the organization.';
            if (response.status === 400) {
              message += ' Message: ' + response.data.message;
            }

            notificationService.error(message);
          }

          return organizationService.remove(organization.id).then(onSuccess, onFailure);
        });
      }


      vm.add = add;
      vm.get = get;
      vm.leave = leave;
      vm.nextPage = nextPage;
      vm.open = open;
      vm.organizations = [];
      vm.previousPage = previousPage;
      vm.remove = remove;

      get();
    }
    ]);
}());
