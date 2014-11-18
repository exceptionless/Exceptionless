(function () {
  'use strict';

  angular.module('exceptionless.dialog')
    .factory('dialogService', ['dialogs', function (dialogs) {
      function confirm(message, confirmButtonText) {
        return dialogs.create('components/dialog/confirm-dialog.tpl.html', 'confirmDialog as vm', {
          message: message,
          confirmButtonText: confirmButtonText
        }).result;
      }

      function confirmDanger(message, confirmButtonText) {
        return dialogs.create('components/dialog/confirm-dialog.tpl.html', 'confirmDialog as vm', {
          message: message,
          confirmButtonText: confirmButtonText,
          danger: true
        }).result;
      }

      function confirmUpgradePlan(message) {
        return confirm(message, 'Upgrade Plan').then(changePlan);
      }

      function changePlan() {
      }

      var service = {
        confirm: confirm,
        confirmDanger: confirmDanger,
        confirmUpgradePlan: confirmUpgradePlan,
        changePlan: changePlan
      };

      return service;
    }
    ]);
}());
