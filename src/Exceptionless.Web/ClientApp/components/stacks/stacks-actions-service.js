(function () {
  'use strict';

  angular.module('exceptionless.stacks')
    .factory('stacksActionsService', function ($ExceptionlessClient, dialogService, stackDialogService, stackService, notificationService, translateService, $q) {
      var source = 'exceptionless.stacks.stacksActionsService';

      function actionWithParameter(action, parameter) {
        return function(ids) { return action(ids, parameter); };
      }

      function executeAction(ids, action, onSuccess, onFailure) {
        var deferred = $q.defer();
        var promise = _.chunk(ids, 10).reduce(function (previous, item) {
          return previous.then(action(item.join(',')));
        }, deferred.promise).then(onSuccess, onFailure);

        deferred.resolve();
        return promise;
      }

      var markOpenAction = {
        name: 'Mark Open',
        run: function (ids) {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully marked the stacks as open.'));
          }

          function onFailure() {
            $ExceptionlessClient.createFeatureUsage(source + '.mark-open.error').setProperty('count', ids.length).submit();
            notificationService.error(translateService.T('An error occurred while marking stacks as open.'));
          }

          $ExceptionlessClient.createFeatureUsage(source + '.mark-open').setProperty('count', ids.length).submit();
          return executeAction(ids, actionWithParameter(stackService.changeStatus, "open"), onSuccess, onFailure);
        }
      };

      var deleteAction = {
        name: 'Delete',
        run: function (ids) {
          $ExceptionlessClient.createFeatureUsage(source + '.delete').setProperty('count', ids.length).submit();
          return dialogService.confirmDanger(translateService.T('Are you sure you want to delete these stacks (includes all stack events)?'), translateService.T('DELETE STACKS')).then(function () {
            function onSuccess() {
              notificationService.info(translateService.T('Successfully queued the stacks for deletion.'));
            }

            function onFailure() {
              $ExceptionlessClient.createFeatureUsage(source + '.delete.error').setProperty('count', ids.length).submit();
              notificationService.error(translateService.T('An error occurred while deleting the stacks.'));
            }

            return executeAction(ids, stackService.remove, onSuccess, onFailure);
          }).catch(function(e){});
        }
      };

      var markFixedAction = {
        name: 'Mark Fixed',
        run: function (ids) {
          $ExceptionlessClient.createFeatureUsage(source + '.mark-fixed').setProperty('count', ids.length).submit();
          return stackDialogService.markFixed().then(function (version) {
            function onSuccess() {
              notificationService.info(translateService.T('Successfully marked the stacks as fixed.'));
            }

            function onFailure() {
              $ExceptionlessClient.createFeatureUsage(source + '.mark-fixed.error').setProperty('count', ids.length).submit();
              notificationService.error(translateService.T('An error occurred while marking stacks as fixed.'));
            }

            return executeAction(ids, actionWithParameter(stackService.markFixed, version), onSuccess, onFailure);
          }).catch(function(e){});
        }
      };

      var markIgnoredAction = {
        name: 'Mark Ignored',
        run: function (ids) {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully marked the stacks as ignored.'));
          }

          function onFailure() {
            $ExceptionlessClient.createFeatureUsage(source + '.mark-ignored.error').setProperty('count', ids.length).submit();
            notificationService.error(translateService.T('An error occurred while marking stacks as ignored.'));
          }

          $ExceptionlessClient.createFeatureUsage(source + '.mark-ignored').setProperty('count', ids.length).submit();
          return executeAction(ids, actionWithParameter(stackService.changeStatus, "ignored"), onSuccess, onFailure);
        }
      };

      var markDiscardedAction = {
        name: 'Mark Discarded',
        run: function (ids) {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully marked the stacks as discarded.'));
          }

          function onFailure() {
            $ExceptionlessClient.createFeatureUsage(source + '.mark-discarded.error').setProperty('count', ids.length).submit();
            notificationService.error(translateService.T('An error occurred while marking stacks as discarded.'));
          }

          $ExceptionlessClient.createFeatureUsage(source + '.mark-discarded').setProperty('count', ids.length).submit();
          return executeAction(ids, actionWithParameter(stackService.changeStatus, "discarded"), onSuccess, onFailure);
        }
      };

      function getActions() {
        return [markOpenAction, markFixedAction, markIgnoredAction, markDiscardedAction, deleteAction];
      }

      var service = {
        getActions: getActions
      };

      return service;
    });
}());
