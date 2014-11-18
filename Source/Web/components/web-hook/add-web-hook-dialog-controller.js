(function () {
  'use strict';

  angular.module('exceptionless.web-hook')
    .controller('AddWebHookDialog', ['$modalInstance', function ($modalInstance) {
      var vm = this;

      function cancel() {
        $modalInstance.dismiss('cancel');
      }

      function getEventTypes() {
        return [
          {
            key: 'NewEvent',
            name: 'New Event',
            description: 'Occurs when a new event that has never been seen before is reported to your project.'
          },
          {
            key: 'CriticalEvent',
            name: 'Critical Event',
            description: 'Occurs when an event that has been marked as critical is reported to your project.'
          },
          {key: 'StackPromoted', name: 'Promoted', description: 'Used to promote event stacks to external systems.'},
          {
            key: 'NewError',
            name: 'New Error',
            description: 'Occurs when a new error that has never been seen before is reported to your project.'
          },
          {
            key: 'CriticalError',
            name: 'Critical Event',
            description: 'Occurs when an error that has been marked as critical is reported to your project.'
          },
          {
            key: 'ErrorRegression',
            name: 'Regression',
            description: 'Occurs when an error that has been marked as fixed is reported to your project.'
          }
        ];
      }

      function hasEventTypeSelection() {
        return vm.data.event_types && vm.data.event_types.length > 0;
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        $modalInstance.close(vm.data);
      }

      vm.cancel = cancel;
      vm.data = {};
      vm.eventTypes = getEventTypes();
      vm.hasEventTypeSelection = hasEventTypeSelection;
      vm.save = save;
    }]);
}());
