(function () {
  'use strict';

  angular.module('exceptionless.stack-dialog')
    .factory('stackDialogService', function (dialogs) {
      function markFixed() {
        return dialogs.create('components/stack-dialog/mark-fixed-dialog.tpl.html', 'MarkFixedDialog as vm').result;
      }

      var service = {
        markFixed: markFixed
      };

      return service;
    });
}());
