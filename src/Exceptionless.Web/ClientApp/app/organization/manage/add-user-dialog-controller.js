(function () {
  'use strict';

  angular.module('app.organization')
    .controller('AddUserDialog', function ($ExceptionlessClient, $uibModalInstance) {
      var vm = this;
      function cancel() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.cancel');
        $uibModalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('email', vm.data.email).submit();
        $uibModalInstance.close(vm.data.email);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.organization.AddUserDialog';
        vm.cancel = cancel;
        vm.save = save;
        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());
