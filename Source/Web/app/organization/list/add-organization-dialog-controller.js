(function () {
  'use strict';

  angular.module('app.organization')
    .controller('AddOrganizationDialog', ['$modalInstance', function ($modalInstance) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $modalInstance.close(vm.data.name);
      }

      vm.cancel = cancel;
      vm.save = save;
    }]);
}());
