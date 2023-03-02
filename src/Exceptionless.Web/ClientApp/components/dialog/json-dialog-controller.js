(function () {
  'use strict';

  angular.module('exceptionless.dialog')
    .controller('jsonDialog', function ($uibModalInstance, $translate, clipboard, data) {
      var vm = this;
      function close() {
        $uibModalInstance.dismiss('close');
      }

      function copy() {
        clipboard.copyText(vm.json);
        $uibModalInstance.close('copy');
      }

      this.$onInit = function $onInit() {
        vm.copyButtonText = angular.isDefined(data.copyButtonText) ? data.copyButtonText : $translate.instant('Copy to Clipboard');
        vm.header = angular.isDefined(data.header) ? data.header : $translate.instant('DIALOGS_JSON');
        vm.json = data.json;
        vm.close = close;
        vm.copy = copy;
        vm.showCopyButton = clipboard.supported;
      };
    });
}());
