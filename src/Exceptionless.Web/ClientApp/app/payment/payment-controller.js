(function () {
  'use strict';

  angular.module('app.payment')
    .controller('Payment', function ($state, $stateParams, notificationService, organizationService, translateService) {
      var vm = this;
      function getInvoice() {
        function onSuccess(response) {
          vm.invoice = response.data.plain();
          return vm.invoice;
        }

        function onFailure() {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Invoice', { invoiceId : vm._invoiceId }));
        }

        return organizationService.getInvoice(vm._invoiceId).then(onSuccess, onFailure);
      }

      this.$onInit = function $onInit() {
        vm._invoiceId = $stateParams.id;
        vm.invoice = {};

        getInvoice();
      };
    });
}());
