(function () {
  'use strict';

  angular.module('app')
    .controller('app.New', ['stackService', function (stackService) {
      var vm = this;
      vm.newest = {
        get: function (options) {
          return stackService.getNew(options);
        },
        options: {
          limit: 20,
          mode: 'summary'
        }
      };
    }
    ]);
}());
