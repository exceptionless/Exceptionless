(function () {
  'use strict';

  angular.module('app.event')
    .controller('Reference', function (eventService, $state, $stateParams) {
      var vm = this;

      vm.references = {
        get: function(options) {
          return eventService.getByReferenceId($stateParams.referenceId, options).then(function(response) {
            var events = response.data.plain();
            if (events && events.length === 1) {
              $state.go('app.event', { id: events[0].id });
            }

            return response;
          });
        },
        options: {
          limit: 20,
          mode: 'summary'
        },
        source: 'app.event.Reference'
      };
    });
}());
