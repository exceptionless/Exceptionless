(function () {
  'use strict';

  angular.module('exceptionless.release-notification', [
    'exceptionless.refresh'
  ])
  .directive('releaseNotification', [function() {
    return {
      restrict: 'E',
      templateUrl: "components/release-notification/release-notification-directive.tpl.html",
      controller: function($window) {
        var rvm = this;
        function processReleaseNotification(notification) {
          if (notification) {
            if (notification.critical) {
              $window.location.reload();
            }

            rvm.releaseNotificationMessage = notification.message;
          }
        }

        this.$onInit = function $onInit() {
          rvm.processReleaseNotification = processReleaseNotification;
          rvm.releaseNotificationMessage = '';
        };
      },
      controllerAs: 'rvm'
    };
  }]);
}());

