(function () {
  'use strict';

  angular.module('exceptionless.dialog')
    .factory('dialogService', function (dialogs) {
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

      function viewJSON(json, copyButtonText) {
        return dialogs.create('components/dialog/json-dialog.tpl.html', 'jsonDialog as vm', {
          copyButtonText: copyButtonText,
          json: json
        }).result;
      }

      return {
        confirm: confirm,
        confirmDanger: confirmDanger,
        viewJSON: viewJSON
      };
    });
}());
