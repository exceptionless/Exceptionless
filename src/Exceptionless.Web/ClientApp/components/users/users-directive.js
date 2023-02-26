(function () {
  'use strict';

  angular.module('exceptionless.users', [
    'exceptionless',
    'exceptionless.dialog',
    'exceptionless.link',
    'exceptionless.notification',
    'exceptionless.organization',
    'exceptionless.pagination',
    'exceptionless.refresh',
    'exceptionless.user'
  ])
    .directive('users', function () {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: "="
        },
        templateUrl: 'components/users/users-directive.tpl.html',
        controller: function ($ExceptionlessClient, $window, $state, dialogService, linkService, notificationService, organizationService, paginationService, userService, translateService) {
          var vm = this;
          function get(options, useCache) {
            function onSuccess(response) {
              vm.users = response.data.plain();

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];

              vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

              if (vm.users.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
                return get(null, useCache);
              }

              return vm.users;
            }

            vm.currentOptions = options || vm.settings.options;
            return vm.settings.get(vm.currentOptions, useCache).then(onSuccess);
          }

          function hasAdminRole(user) {
            return userService.hasAdminRole(user);
          }

          function hasUsers() {
            return vm.users && vm.users.length > 0;
          }

          function nextPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.nextPage').setProperty('next', vm.next).submit();
            return get(vm.next);
          }

          function previousPage() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.previousPage').setProperty('previous', vm.previous).submit();
            return get(vm.previous);
          }

          function remove(user) {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove').setProperty('user', user).submit();
            return dialogService.confirmDanger(translateService.T('Are you sure you want to remove this user from your organization?'), translateService.T('Remove User')).then(function () {
              function onSuccess() {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.success').setProperty('user', user).submit();
              }

              function onFailure() {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.error').setProperty('user', user).submit();
                notificationService.error(translateService.T('An error occurred while trying to remove the user.'));
              }

              return organizationService.removeUser(vm.settings.organizationId, user.email_address).then(onSuccess, onFailure);
            }).catch(function(e){});
          }

          function resendNotification(user) {
            function onFailure() {
              notificationService.error(translateService.T('An error occurred while trying to resend the notification.'));
            }

            return organizationService.addUser(vm.settings.organizationId, user.email_address).catch(onFailure);
          }

          function updateAdminRole(user) {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.updateAdminRole').setProperty('user', user).submit();
            var message = (!userService.hasAdminRole(user) ? 'Are you sure you want to add the admin role for this user?' : 'Are you sure you want to remove the admin role from this user?');
            return dialogService.confirmDanger(translateService.T(message), translateService.T(!userService.hasAdminRole(user) ? 'Add' : 'Remove')).then(function () {
              function onSuccess() {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.updateAdminRole.success').setProperty('user', user).submit();
              }

              function onFailure() {
                $ExceptionlessClient.createFeatureUsage(vm._source + '.updateAdminRole.error').setProperty('user', user).submit();
                notificationService.error(translateService.T(!userService.hasAdminRole(user) ? 'An error occurred while add the admin role.' : 'An error occurred while remove the admin role.'));
              }

              if (!userService.hasAdminRole(user)) {
                return userService.addAdminRole(user.id).then(onSuccess, onFailure);
              }

              return userService.removeAdminRole(user.id).then(onSuccess, onFailure);
            }).catch(function(e){});
          }

          this.$onInit = function $onInit() {
            vm._source = 'exceptionless.users';
            vm.currentOptions = {};
            vm.get = get;
            vm.hasAdminRole = hasAdminRole;
            vm.hasUsers = hasUsers;
            vm.nextPage = nextPage;
            vm.open = open;
            vm.previousPage = previousPage;
            vm.remove = remove;
            vm.resendNotification = resendNotification;
            vm.updateAdminRole = updateAdminRole;
            vm.users = [];

            $ExceptionlessClient.submitFeatureUsage(vm._source);
            get();
          };
        },
        controllerAs: 'vm'
      };
    });
}());
