(function () {
  'use strict';

  angular.module('exceptionless.web-hook')
    .controller('AddWebHookDialog', function ($ExceptionlessClient, $uibModalInstance, translateService) {
      var vm = this;
      function cancel() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.cancel');
        $uibModalInstance.dismiss('cancel');
      }

      function getEventTypes() {
        return [
          {
            key: 'NewError',
            name: translateService.T('New Error'),
            description: translateService.T('Occurs when a new error that has never been seen before is reported to your project.')
          },
          {
            key: 'CriticalError',
            name: translateService.T('Critical Error'),
            description: translateService.T('Occurs when an error that has been marked as critical is reported to your project.')
          },
          {
            key: 'StackRegression',
            name: translateService.T('Regression'),
            description: translateService.T('Occurs when an event that has been marked as fixed has reoccurred in your project.')
          },
          {
            key: 'NewEvent',
            name: translateService.T('New Event'),
            description: translateService.T('Occurs when a new event that has never been seen before is reported to your project.')
          },
          {
            key: 'CriticalEvent',
            name: translateService.T('Critical Event'),
            description: translateService.T('Occurs when an event that has been marked as critical is reported to your project.')
          },
          {
            key: 'StackPromoted',
            name: translateService.T('Promoted'),
            description: translateService.T('Used to promote event stacks to external systems.')
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

        $ExceptionlessClient.createFeatureUsage(vm._source + '.save').setProperty('WebHook', vm.data).submit();
        $uibModalInstance.close(vm.data);
      }

      this.$onInit = function $onInit() {
        vm._source = 'exceptionless.web-hook.AddWebHookDialog';
        vm.addWebHookForm = {};
        vm.cancel = cancel;
        vm.data = {};
        vm.eventTypes = getEventTypes();
        vm.hasEventTypeSelection = hasEventTypeSelection;
        vm.save = save;

        $ExceptionlessClient.submitFeatureUsage(vm._source);
      };
    });
}());
