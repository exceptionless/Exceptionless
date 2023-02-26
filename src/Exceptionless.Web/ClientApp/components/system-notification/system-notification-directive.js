(function () {
  'use strict';

  angular.module('exceptionless.system-notification', [
    'exceptionless.refresh'
  ])
  .directive('systemNotification', [function(SYSTEM_NOTIFICATION_MESSAGE) {
    return {
      restrict: 'E',
      templateUrl: "components/system-notification/system-notification-directive.tpl.html",
      controller: function() {
        var svm = this;
        function processSystemNotification(notification) {
          if (notification) {
            svm.systemNotificationMessage = notification.message || SYSTEM_NOTIFICATION_MESSAGE;
          }
        }

        this.$onInit = function $onInit() {
          svm.processSystemNotification = processSystemNotification;
          svm.systemNotificationMessage = SYSTEM_NOTIFICATION_MESSAGE;
        };
      },
      controllerAs: 'svm'
    };
  }]);
}());

