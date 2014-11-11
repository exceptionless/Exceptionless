(function () {
  'use strict';

  angular.module('app.stack')
    .controller('AddReferenceDialog', ['$modalInstance', function ($modalInstance) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $modalInstance.close(vm.data.url);
      }

      vm.cancel = cancel;
      vm.save = save;
    }]);
}());
