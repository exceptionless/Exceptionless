(function () {
  'use strict';

  angular.module('app')
    .controller('app.Frequent', ['stackService', function (stackService) {
      var vm = this;
      vm.mostFrequent = {
        get: function (options) {
          return stackService.getFrequent(options);
        },
        options: {
          limit: 20,
          mode: 'summary'
        }
      };
    }
    ]);
}());
