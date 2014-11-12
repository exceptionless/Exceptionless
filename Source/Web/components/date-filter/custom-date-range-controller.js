(function () {
  'use strict';

  angular.module('exceptionless.date-filter')
    .controller('CustomDateRangeDialog', ['$modalInstance', function ($modalInstance) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function save() {
        $modalInstance.close(vm.data);
      }

      vm.cancel = cancel;
      vm.save = save;
    }]);
}());
