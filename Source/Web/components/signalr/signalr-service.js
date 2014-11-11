(function () {
  'use strict';

  angular.module('exceptionless.signalr', ['SignalR'])
    .factory('signalRService', ['$rootScope', '$timeout', '$log', 'Hub', function ($rootScope, $timeout, $log, Hub) {
      var signalR;

      function startDelayed(baseUrl, accessToken) {
        if (signalR)
          stop();

        signalR = $timeout(function () {
          var hub = new Hub('message-bus', {
            rootPath: baseUrl + '/signalr/hubs',


            // client side methods
            listeners: {
              'entityChanged': function (entityChanged) {
                $rootScope.$emit(entityChanged.type + 'Changed', entityChanged);
              },
              'eventOccurrence': function (eventOccurrence) {
                $rootScope.$emit('eventOccurrence', eventOccurrence);
              },
              'stackUpdated': function (stackUpdated) {
                $rootScope.$emit('stackUpdated', stackUpdated);
              },
              'planOverage': function (planOverage) {
                $rootScope.$emit('planOverage', planOverage);
              },
              'planChanged': function (planChanged) {
                $rootScope.$emit('planChanged', planChanged);
              }
            },

            // query params sent on initial connection
            queryParams: {
              'access_token': accessToken // TODO: Inject this.
            },

            // handle connection error
            errorHandler: function (error) {
              $log.error(error);
            }
          });
        }, 1000);
      }

      function stop() {
        if (!signalR)
          return;

        $timeout.cancel(signalR);
        signalR = null;
      }

      var service = {
        startDelayed: startDelayed,
        stop: stop
      };

      return service;
    }]);
}());
