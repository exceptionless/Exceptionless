(function () {
    'use strict';

    angular.module('exceptionless.stack-actions', [])
        .factory('stackActionsService', ['stackService', 'notificationService', function (stackService, notificationService) {
            var markFixedAction = {
                name: 'Mark Fixed',
                run: function (id) {
                    return stackService.markFixed(id).then(function(response) {
                        notificationService.success('Successfully marked stacks as fixed.');
                    }, function () {
                        notificationService.error('An error occurred while marking stacks as fixed.');
                    });
                }
            };

            var markHiddenAction = {
                name: 'Mark Hidden',
                run: function(id) {
                    return stackService.markHidden(id).then(function(response) {
                        notificationService.success('Successfully marked stacks as hidden.');
                    }, function () {
                        notificationService.error('An error occurred while marking stacks as hidden.');
                    });
                }
            };

            function getActions() {
                return [markFixedAction, markHiddenAction];
            }

            var service = {
                getActions: getActions
            };

            return service;
        }]);
}());