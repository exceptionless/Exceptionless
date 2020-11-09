(function () {
  'use strict';

  angular.module('app.organization')
    .directive('invoices', function () {
      return {
        bindToController: true,
        restrict: 'E',
        replace: true,
        scope: {
          settings: "="
        },
        templateUrl: 'app/organization/manage/invoices-directive.tpl.html',
        controller: function ($ExceptionlessClient, $window, $state, linkService, notificationService, paginationService, userService) {
          var vm = this;

          function get(options, useCache) {
            function onSuccess(response) {
              vm.invoices = response.data.plain();

              var links = linkService.getLinksQueryParameters(response.headers('link'));
              vm.previous = links['previous'];
              vm.next = links['next'];

              vm.pageSummary = paginationService.getCurrentPageSummary(response.data, vm.currentOptions.page, vm.currentOptions.limit);

              if (vm.invoices.length === 0 && vm.currentOptions.page && vm.currentOptions.page > 1) {
                return get(null, useCache);
              }

              return vm.invoices;
            }

            vm.currentOptions = options || vm.settings.options;
            return vm.settings.get(vm.currentOptions, useCache).then(onSuccess).catch(function(e){});
          }

          function hasAdminRole(user) {
            return userService.hasAdminRole(user);
          }

          function open(id) {
            $ExceptionlessClient.createFeatureUsage(vm.source + '.open').setProperty('id', id).submit();
            $window.open($state.href('payment', { id: id }, { absolute: true }), '_blank');
          }

          function nextPage() {
            $ExceptionlessClient.createFeatureUsage(vm.source + '.nextPage').setProperty('next', vm.next).submit();
            return get(vm.next);
          }

          function previousPage() {
            $ExceptionlessClient.createFeatureUsage(vm.source + '.previousPage').setProperty('previous', vm.previous).submit();
            return get(vm.previous);
          }

          this.$onInit = function $onInit() {
            vm.source = 'exceptionless.organization.invoices';
            vm.currentOptions = {};
            vm.get = get;
            vm.hasAdminRole = hasAdminRole;
            vm.nextPage = nextPage;
            vm.open = open;
            vm.previousPage = previousPage;
            vm.invoices = [];

            $ExceptionlessClient.submitFeatureUsage(vm.source);
            get();
          };
        },
        controllerAs: 'vm'
      };
    });
}());
