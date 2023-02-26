(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.Add', function ($state, $stateParams, $timeout, billingService, organizationService, projectService, notificationService, translateService) {
      var vm = this;
      function add(isRetrying) {
        function resetCanAdd() {
          vm._canAdd = true;
        }

        function retry(delay) {
          var timeout = $timeout(function() {
            $timeout.cancel(timeout);
            add(true);
          }, delay || 100);
        }

        if (!vm.addForm || vm.addForm.$invalid) {
          resetCanAdd();
          return !isRetrying && retry(1000);
        }

        if ((canCreateOrganization() && !vm.organization_name) || !vm.project_name || vm.addForm.$pending) {
          return retry();
        }

        if (vm._canAdd) {
          vm._canAdd = false;
        } else {
          return;
        }

        if (canCreateOrganization()) {
          return createOrganization(vm.organization_name).then(createProject).then(resetCanAdd, resetCanAdd);
        }

        return createProject(vm.currentOrganization).then(resetCanAdd, resetCanAdd);
      }

      function canCreateOrganization() {
        return vm.currentOrganization.id === vm._newOrganizationId || !hasOrganizations();
      }

      function createOrganization(name) {
        function onSuccess(response) {
          vm.organizations.push(response.data);
          vm.currentOrganization = response.data;
          return response.data;
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

      function createProject(organization) {
        if (!organization) {
          vm._canAdd = true;
          return;
        }

        function onSuccess(response) {
          $state.go('app.project.configure', { id: response.data.id, redirect: true });
        }

        function onFailure(response) {
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message, organization.id).then(function () {
              return createProject(organization);
            }).catch(function(e){});
          }

          var message = translateService.T('An error occurred while creating the project.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return projectService.create(organization.id, vm.project_name).then(onSuccess, onFailure);
      }

      function getOrganizations() {
        function onSuccess(response) {
          vm.organizations = response.data;
          vm.organizations.push({id: vm._newOrganizationId, name: translateService.T('<New Organization>')});

          var currentOrganizationId = vm.currentOrganization.id ? vm.currentOrganization.id : $stateParams.organizationId;
          vm.currentOrganization = vm.organizations.filter(function(o) { return o.id === currentOrganizationId; })[0];
          if (!vm.currentOrganization) {
            vm.currentOrganization = vm.organizations.length > 0 ? vm.organizations[0] : {};
          }
        }

        organizationService.getAll().then(onSuccess);
      }

      function hasOrganizations() {
        return vm.organizations.filter(function (o) {
            return o.id !== vm._newOrganizationId;
          }).length > 0;
      }

      this.$onInit = function $onInit() {
        vm._canAdd = true;
        vm._newOrganizationId = '__newOrganization';
        vm.add = add;
        vm.addForm = {};
        vm.canCreateOrganization = canCreateOrganization;
        vm.currentOrganization = {};
        vm.getOrganizations = getOrganizations;
        vm.hasOrganizations = hasOrganizations;
        vm.organizations = [];

        getOrganizations();
      };
    });
}());
