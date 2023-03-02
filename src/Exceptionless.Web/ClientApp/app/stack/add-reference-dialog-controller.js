(function () {
  'use strict';

  angular.module('app.stack')
    .controller('AddReferenceDialog', function ($ExceptionlessClient, $uibModalInstance) {
      var vm = this;
      function cancel() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.cancel');
        $uibModalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('url', vm.data.url).submit();
        $uibModalInstance.close(vm.data.url);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.stack.AddReferenceDialog';
        vm.cancel = cancel;
        vm.save = save;
        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());
