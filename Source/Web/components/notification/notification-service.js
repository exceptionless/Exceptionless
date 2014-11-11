(function () {
  'use strict';

  angular.module('exceptionless.notification', ['toaster'])
    .factory('notificationService', ['toaster', function (toaster) {
      function error(title, text) {
        toaster.pop('error', title, text, 5000);
      }

      function info(title, text) {
        toaster.pop('note', title, text, 3000);
      }

      function success(title, text) {
        toaster.pop('success', title, text, 3000);
      }

      function wait(title, text) {
        toaster.pop('wait', title, text, 3000);
      }

      function warning(title, text) {
        toaster.pop('warning', title, text, 3000);
      }

      var service = {
        error: error,
        info: info,
        success: success,
        wait: wait,
        warning: warning
      };

      return service;
    }]);
}());
