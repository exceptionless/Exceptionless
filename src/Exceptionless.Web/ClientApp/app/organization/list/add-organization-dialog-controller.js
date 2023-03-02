(function () {
  'use strict';

  angular.module('app.organization')
    .controller('AddOrganizationDialog', function ($ExceptionlessClient, $uibModalInstance, $timeout) {
      var vm = this;

      function cancel() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.cancel');
        $uibModalInstance.dismiss('cancel');
      }

      function save(isRetrying) {
        function retry(delay) {
          var timeout = $timeout(function() {
            $timeout.cancel(timeout);
            save(true);
          }, delay || 100);
        }

        if (!vm.addOrganizationForm || vm.addOrganizationForm.$invalid) {
          vm._canSave = true;
          return !isRetrying && retry(1000);
        }

        if (!vm.data.name || vm.addOrganizationForm.$pending) {
          return retry();
        }

        if (vm._canSave) {
          vm._canSave = false;
        } else {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('name', vm.data.name).submit();
        $uibModalInstance.close(vm.data.name);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.organization.AddOrganizationDialog';
        vm._canSave = true;
        vm.addOrganizationForm = {};
        vm.cancel = cancel;
        vm.data = {};
        vm.save = save;
        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());
