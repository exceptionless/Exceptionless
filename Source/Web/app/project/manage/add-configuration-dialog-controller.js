(function () {
  'use strict';

  angular.module('app.project')
    .controller('AddConfigurationDialog', ['$modalInstance', function ($modalInstance, configuration) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $modalInstance.close(vm.data);
      }

      vm.cancel = cancel;
      vm.configuration = configuration;
      vm.data = {};
      vm.save = save;
    }]);
}());
