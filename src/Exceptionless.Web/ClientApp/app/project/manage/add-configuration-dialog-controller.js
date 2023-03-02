(function () {
  'use strict';

  angular.module('app.project')
    .controller('AddConfigurationDialog', function ($ExceptionlessClient, $uibModalInstance, data) {
      var vm = this;
      function cancel() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.cancel');
        $uibModalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('configuration', vm.data).submit();
        $uibModalInstance.close(vm.data);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.project.AddConfigurationDialog';
        vm.cancel = cancel;
        vm.configuration = data;
        vm.data = {};
        vm.save = save;
        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());
