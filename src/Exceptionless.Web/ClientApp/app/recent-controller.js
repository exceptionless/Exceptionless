(function () {
  'use strict';

  angular.module('app')
    .controller('app.Recent', function (eventService) {
      var vm = this;
      this.$onInit = function $onInit() {
        vm.mostRecent = {
          get: eventService.getAll,
          options: {
            limit: 20,
            mode: 'summary'
          },
          source: 'app.Recent'
        };
      };
    });
}());
