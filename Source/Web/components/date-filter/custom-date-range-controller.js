(function () {
  'use strict';

  angular.module('exceptionless.date-filter')
    .controller('CustomDateRangeDialog', ['$modalInstance', function ($modalInstance) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function save() {
        if (!vm.range.start || !vm.range.end) {
          return;
        }

        $modalInstance.close(vm.range);
      }

      vm.cancel = cancel;
      vm.options =  {
        autoclose: true,
        startDate: '01/01/2012',
        endDate: new Date(),
        todayBtn: 'linked',
        todayHighlight: true
      };
      vm.range = {};
      vm.save = save;
    }]);
}());
