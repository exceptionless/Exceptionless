(function () {
  'use strict';

  angular.module('exceptionless.rate-limit')
    .directive('rateLimit', function() {
      return {
        restrict: 'E',
        replace: true,
        scope: {
          organizationId: '=',
          ignoreFree: '=',
          ignoreConfigureProjects: '='
        },
        templateUrl: "components/rate-limit/rate-limit-directive.tpl.html",
        controller: function(rateLimitService) {
          var vm = this;
          function rateLimitExceeded() {
            return rateLimitService.rateLimitExceeded();
          }

          this.$onInit = function $onInit() {
            vm.rateLimitExceeded = rateLimitExceeded;
          };
        },
        controllerAs: 'vm'
      };
    });
}());

