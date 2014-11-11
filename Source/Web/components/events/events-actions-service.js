(function () {
  'use strict';

  angular.module('exceptionless.events')
    .factory('eventsActionsService', ['dialogService', 'eventService', 'notificationService', function (dialogService, eventService, notificationService) {
      var deleteAction = {
        name: 'Delete',
        run: function (ids) {
          return dialogService.confirmDanger('Are you sure you want to delete these events', 'DELETE EVENTS').then(function () {
            function onSuccess() {
              notificationService.success('Successfully deleted the events.');
            }

            function onFailure() {
              notificationService.error('An error occurred while deleting the events.');
            }

            return eventService.remove(ids.join(',')).then(onSuccess, onFailure);
          });
        }
      };

      function getActions() {
        return [deleteAction];
      }

      var service = {
        getActions: getActions
      };

      return service;
    }]);
}());
