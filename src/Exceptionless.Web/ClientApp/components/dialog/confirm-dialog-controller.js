(function () {
  'use strict';

  angular.module('exceptionless.dialog')
    .controller('confirmDialog', function ($uibModalInstance, $translate, data) {
      var vm = this;
      function cancel() {
        $uibModalInstance.dismiss('cancel');
      }

      function confirm() {
        $uibModalInstance.close('confirm');
      }

      this.$onInit = function $onInit() {
        vm.confirmButtonText = angular.isDefined(data.confirmButtonText) ? data.confirmButtonText : $translate.instant('DIALOGS_YES');
        vm.danger = angular.isDefined(data.danger) && data.danger === true;
        vm.header = angular.isDefined(data.header) ? data.header : $translate.instant('DIALOGS_CONFIRMATION');
        vm.message = angular.isDefined(data.message) ? data.message : $translate.instant('DIALOGS_CONFIRMATION_MSG');
        vm.cancel = cancel;
        vm.confirm = confirm;
      };
    });
}());
