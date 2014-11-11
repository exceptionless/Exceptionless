(function () {
  'use strict';

  angular.module('app')
    .controller('app.Recent', ['eventService', function (eventService) {
      var vm = this;
      vm.mostRecent = {
        get: function (options) {
          return eventService.getAll(options);
        },
        options: {
          limit: 20,
          mode: 'summary'
        }
      };
    }
    ]);
}());
