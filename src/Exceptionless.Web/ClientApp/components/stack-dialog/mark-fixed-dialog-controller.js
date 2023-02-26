(function () {
  'use strict';

  angular.module('exceptionless.stack-dialog')
    .controller('MarkFixedDialog', function ($ExceptionlessClient, $uibModalInstance) {
      var vm = this;
      function cancel() {
        $uibModalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (isValid === false) {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('version', vm.data.version).submit();
        $uibModalInstance.close(vm.data.version);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.stack-dialog.MarkFixedDialog';
        vm.cancel = cancel;
        vm.data = {};
        vm.markFixedForm = {};
        vm.save = save;
        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());

