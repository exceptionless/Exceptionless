(function () {
  'use strict';

  angular.module('app.reports')
    .controller('reports.status', function ($stateParams, stackService) {
      var vm = this;

      function getIcon(status) {
        switch(status) {
          case 'regressed':
            return 'fa-bug';
          case 'fixed':
            return 'fa-wrench';
          case 'snoozed':
            return 'fa-clock-o';
          case 'ignored':
            return 'fa-trash-o';
          case 'discarded':
            return 'fa-ban';
          default:
            return 'fa-bug';
        }
      }

      this.$onInit = function $onInit() {
        vm.status = $stateParams.status;
        vm.icon = getIcon(vm.status);
        vm.stacks = {
          get: stackService.getAll,
          options: {
            limit: 20,
            mode: 'summary'
          },
          source: 'app.Reports.Status'
        };
      };
    });
}());
