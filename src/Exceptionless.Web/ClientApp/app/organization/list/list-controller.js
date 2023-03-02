(function () {
  'use strict';

  angular.module('app.organization')
    .controller('organization.List', function ($ExceptionlessClient, $rootScope, $scope, $window, $state, billingService, dialogs, dialogService, filterService, linkService, notificationService, organizationService, paginationService, translateService, STRIPE_PUBLISHABLE_KEY) {
      var vm = this;
      function add() {
        return dialogs.create('app/organization/list/add-organization-dialog.tpl.html', 'AddOrganizationDialog as vm').result.then(createOrganization).catch(function(e){});
      }

      function changePlan(organizationId) {
        if (!STRIPE_PUBLISHABLE_KEY) {
          notificationService.error(translateService.T('Billing is currently disabled.'));
          return;
        }

        return billingService.changePlan(organizationId).catch(function(e){});
      }

      function createOrganization(name) {
        function onSuccess(response) {
          vm.organizations.push(response.data.plain());
          vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organizations.length > 0;
        }

        function onFailure(response) {
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message).then(function () {
              return createOrganization(name);
            }).catch(function(e){});
          }

          var message = translateService.T('An error occurred while creating the organization.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return organizationService.create(name).then(onSuccess, onFailure);

      }

      function get(options, useCache) {
        function onSuccess(response) {
          vm.organizations = response.data.plain();
          vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organizations.length > 0;

          var links = linkService.getLinksQueryParameters(response.headers('link'));
          vm.previous = links['previous'];
          vm.next = links['next'];

          vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

          if (vm.organizations.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
            return get(null, useCache);
          }

          return vm.organizations;
        }

        vm.loading = vm.organizations.length === 0;
        vm.currentOptions = options || vm._settings;
        return organizationService.getAll(vm.currentOptions, useCache).then(onSuccess).finally(function() {
          vm.loading = false;
        });
      }

      function leave(organization, user) {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to leave this organization?'), translateService.T('Leave Organization')).then(function () {
          function onSuccess() {
            vm.organizations.splice(vm.organizations.indexOf(organization), 1);
            vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organizations.length > 0;
          }

          function onFailure(response) {
            var message = translateService.T('An error occurred while trying to leave the organization.');
            if (response.status === 400) {
              message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
            }

            notificationService.error(message);
          }

          return organizationService.removeUser(organization.id, user.email_address).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function open(id, event) {
        var openInNewTab = (event.ctrlKey || event.metaKey || event.which === 2);
        $ExceptionlessClient.createFeatureUsage(vm._source + '.open').setProperty('id', id).setProperty('_blank', openInNewTab).submit();
        if (openInNewTab) {
          $window.open($state.href('app.organization.manage', { id: id }, { absolute: true }), '_blank');
        } else {
          $state.go('app.organization.manage', { id: id });
        }

        event.preventDefault();
      }

      function nextPage() {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.nextPage').setProperty('next', vm.next).submit();
        return get(vm.next);
      }

      function previousPage() {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.previousPage').setProperty('previous', vm.previous).submit();
        return get(vm.previous);
      }

      function remove(organization) {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.remove').setProperty('organization', organization).submit();
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this organization?'), translateService.T('Delete Organization')).then(function () {
          function onSuccess() {
            vm.organizations.splice(vm.organizations.indexOf(organization), 1);
            vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organizations.length > 0;
            notificationService.info(translateService.T('Successfully queued the organization for deletion.'));
            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.success').setProperty('organization', organization).submit();
          }

          function onFailure(response) {
            var message = translateService.T('An error occurred while trying to delete the organization.');
            if (response.status === 400) {
              message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
            }

            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.error').setProperty('organization', organization).submit();
            notificationService.error(message);
          }

          return organizationService.remove(organization.id).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      this.$onInit = function $onInit() {
        vm._source = 'exceptionless.organization.List';
        vm._settings = { mode: 'stats' };
        vm.add = add;
        vm.canChangePlan = false;
        vm.changePlan = changePlan;
        vm.get = get;
        vm.hasFilter = filterService.hasFilter;
        vm.leave = leave;
        vm.loading = true;
        vm.nextPage = nextPage;
        vm.open = open;
        vm.organizations = [];
        vm.previousPage = previousPage;
        vm.remove = remove;

        $ExceptionlessClient.submitFeatureUsage(vm._source);
        get();
      };
    });
}());
