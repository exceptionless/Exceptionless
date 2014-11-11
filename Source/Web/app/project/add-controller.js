(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.Add', ['$state', 'dialogService', 'organizationService', 'projectService', 'notificationService', function ($state, dialogService, organizationService, projectService, notificationService) {
      var newOrganizationId = '__newOrganization';
      var vm = this;

      function add(isValid) {
        if (!isValid) {
          return;
        }

        if (canCreateOrganization()) {
          return createOrganization(vm.organization_name).then(createProject);
        }

        return createProject(vm.currentOrganization);
      }

      function canCreateOrganization() {
        return vm.currentOrganization.id === newOrganizationId || !hasOrganizations();
      }

      function createOrganization(name) {
        function onSuccess(response) {
          vm.organizations.push(response.data);
          vm.currentOrganization = response.data;
          return response.data;
        }

        function onFailure(response) {
          if (response.status === 426) {
            return dialogService.confirmUpgradePlan(response.data.message).then(function () {
              return createOrganization(name);
            });
          }

          notificationService.error('An error occurred while creating the organization');
        }

        return organizationService.create(name).then(onSuccess, onFailure);
      }

      function createProject(organization) {
        if (!organization) {
          return;
        }

        function onSuccess(response) {
          $state.go('app.project.configure', {id: response.data.id});
        }

        function onFailure() {
          notificationService.error('An error occurred while creating the project');
        }

        return projectService.create(organization.id, vm.project_name).then(onSuccess, onFailure);
      }

      function getOrganizations() {
        organizationService.getAll().then(function (response) {
          vm.organizations = response.data;
          vm.organizations.push({id: newOrganizationId, name: '<New Organization>'});
        });
      }

      function hasOrganizations() {
        return vm.organizations.filter(function (o) {
            return o.id !== newOrganizationId;
          }).length > 0;
      }

      vm.add = add;
      vm.canCreateOrganization = canCreateOrganization;
      vm.currentOrganization = {};
      vm.hasOrganizations = hasOrganizations;
      vm.organizations = [];

      getOrganizations();
    }
    ]);
}());
