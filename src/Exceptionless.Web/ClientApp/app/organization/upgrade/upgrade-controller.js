(function () {
  'use strict';

  angular.module('app.organization')
    .controller('organization.Upgrade', function ($ExceptionlessClient, $state, $stateParams, billingService, organizationService, notificationService, translateService, STRIPE_PUBLISHABLE_KEY) {
      var vm = this;

      function get() {
        function onFailure() {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Organization',{organizationId : vm._organizationId}));
        }

        return organizationService.getById(vm._organizationId, false).catch(onFailure);
      }

      function changePlan() {
        function redirect() {
          return $state.go('app.organization.manage', { id: vm._organizationId });
        }

        return billingService.changePlan(vm._organizationId.id).then(redirect, redirect);
      }

      this.$onInit = function $onInit() {
        vm._organizationId = $stateParams.id;
        $ExceptionlessClient.createFeatureUsage('organization.Upgrade').setProperty('OrganizationId', vm._organizationId).submit();

        if (!STRIPE_PUBLISHABLE_KEY) {
          $state.go('app.organization.manage', { id: vm._organizationId });
          return notificationService.error(translateService.T('Billing is currently disabled.'));
        }

        $ExceptionlessClient.submitFeatureUsage('organization.Upgrade');
        return get().then(changePlan);
      };
    });
}());
