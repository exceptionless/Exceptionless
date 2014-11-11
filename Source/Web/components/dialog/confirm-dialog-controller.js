(function () {
  'use strict';

  angular.module('exceptionless.dialog')
    .controller('confirmDialog', ['$modalInstance', '$translate', 'data', function ($modalInstance, $translate, data) {
      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function confirm() {
        $modalInstance.close('confirm');
      }

      var vm = this;
      vm.confirmButtonText = angular.isDefined(data.confirmButtonText) ? data.confirmButtonText : $translate.instant('DIALOGS_YES');
      vm.danger = angular.isDefined(data.danger) && data.danger === true;
      vm.header = angular.isDefined(data.header) ? data.header : $translate.instant('DIALOGS_CONFIRMATION');
      vm.message = angular.isDefined(data.message) ? data.message : $translate.instant('DIALOGS_CONFIRMATION_MSG');
      vm.cancel = cancel;
      vm.confirm = confirm;
    }]);
}());
