(function () {
  'use strict';

  angular.module('app.status')
    .controller('Status', function ($interval, $scope, $state, $stateParams, authService, stateService, statusService, translateService, Restangular) {
      var vm = this;
      var contactSupport = translateService.T('Please contact support for more information.');
      function getMessage() {
        var underMaintenance = translateService.T("We're sorry but the website is currently undergoing maintenance.");

        if (vm._redirect) {
          return underMaintenance + ' ' + translateService.T("You'll be automatically redirected when the maintenance is completed.") + ' ' + contactSupport;
        }

        if (!!vm._message) {
          return vm._message;
        }

        return underMaintenance + ' ' + contactSupport;
      }

      function updateStatus() {
        function updateMessage(response) {
          if (response && response.data && response.data.message) {
            vm._message = response.data.message;
            if (response.status !== 200) {
              vm._message += ' ' + contactSupport;
            }
          } else {
            vm._message = '';
          }
        }

        function onSuccess(response) {
          if (vm._redirect && moment().diff(vm._lastChecked, 'seconds') > 30) {
            if (!authService.isAuthenticated()) {
              return $state.go('auth.login');
            }

            return stateService.restore();
          }


          return updateMessage(response);
        }

        return statusService.get().then(onSuccess, updateMessage);
      }

      this.$onInit = function $onInit() {
        vm._lastChecked = moment();
        vm._message = null;
        vm._redirect = !!$stateParams.redirect;

        var interval = $interval(updateStatus, 30 * 1000);
        $scope.$on('$destroy', function () {
          $interval.cancel(interval);
        });

        vm.getMessage = getMessage;
        updateStatus();
      };
    });
}());
