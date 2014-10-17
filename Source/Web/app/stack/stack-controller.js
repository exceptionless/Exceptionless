(function () {
    'use strict';

    angular.module('app.stack')
        .controller('Stack', ['$state', '$stateParams', 'notificationService', 'featureService', 'dialogs', 'dialogService', 'stackService', 'eventService', function ($state, $stateParams, notificationService, featureService, dialogs, dialogService, stackService, eventService) {
            var stackId = $stateParams.id;
            var vm = this;

            function addReferenceLink(){
                dialogs.create('/app/stack/add-reference-dialog.tpl.html', 'AddReferenceDialog as vm').result.then(function(url) {
                    function onSuccess() {
                        vm.stack.references.push(url);
                    }

                    function onFailure() {
                        notificationService.error('An error occurred while adding the reference link.');
                    }

                    if (vm.stack.references.indexOf(url) < 0)
                        stackService.addLink(stackId, url).then(onSuccess, onFailure);
                });
            }

            function get() {
                stackService.getById(stackId)
                    .then(function (response) {
                        vm.stack = response.data;
                    }, function() {
                        $state.go('app.project.dashboard');
                        notificationService.error('The stack "' + $stateParams.id + '" could not be found.');
                    });
            }

            function hasTags() {
                return vm.stack.tags && vm.stack.tags.length > 0;
            }

            function hasReference() {
                return vm.stack.references && vm.stack.references.length > 0;
            }

            function hasReferences() {
                return vm.stack.references && vm.stack.references.length > 1;
            }

            function isCritical() {
                return vm.stack.occurrences_are_critical === true;
            }

            function isFixed() {
                return vm.stack.date_fixed;
            }

            function isHidden() {
                return vm.stack.is_hidden === true;
            }

            function isRegressed() {
                return vm.stack.is_regressed === true;
            }

            function notificationsDisabled() {
                return vm.stack.disable_notifications === true;
            }

            function promoteToExternal() {
                if (!featureService.hasPremium()) {
                    return dialogService.confirmUpgradePlan('Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.').then(function() {
                        return promoteToExternal();
                    });
                }

                return stackService.promote(stackId)
                    .then(function() {
                        notificationService.success('Successfully promoted stack!');
                    }, function(response) {
                        if (response.status === 426) { // TODO: Move this to an interceptor.
                            return dialogService.confirmUpgradePlan(response.data).then(function() {
                                return promoteToExternal();
                            });
                        }

                        if (response.status === 501) {
                            return dialogService.confirm(response.data, 'Manage Integrations').then(function() {
                                $state.go('app.projects.manage.integrations');
                            });
                        }

                        notificationService.error('An error occurred while promoting this stack.');
                    });
            }

            function removeReferenceLink(reference) {
                return dialogService.confirmDanger('Are you sure you want to remove this reference link?', 'REMOVE REFERENCE LINK').then(function() {
                    function onSuccess() {
                        vm.stack.references.splice(vm.stack.references.indexOf(reference), 1);
                    }

                    function onFailure() {
                        notificationService.info('An error occurred while removing the external reference link.');
                    }

                    return stackService.removeLink(stackId, reference).then(onSuccess, onFailure);
                });
            }

            function resetOccurrences() {
                var message = 'Are you sure you want to reset all occurrences for this stack?';
                return dialogService.confirmDanger(message, 'RESET ALL OCCURRENCE DATA').then(function() {
                    return stackService.resetData(stackId)
                        .then(function (response) {
                            // TODO: Trigger refresh of page data.
                            notificationService.success('Successfully reset the error stacks statistics and occurrences data.');
                        }, function() {
                            notificationService.error('An error occurred while resetting the error stacks statistics and occurrences data.');
                        });
                });
            }

            function updateIsCritical() {
                function onSuccess() {
                    vm.stack.occurrences_are_critical = !isCritical();
                }

                function onFailure() {
                    notificationService.error('An error occurred while marking future occurrences as ' + isCritical() ? 'not critical.' : 'critical.');
                }

                if (isCritical()) {
                    return stackService.markNotCritical(stackId).then(onSuccess, onFailure);
                }

                return stackService.markCritical(stackId).then(onSuccess, onFailure);
            }

            function updateIsFixed(){
                function onSuccess() {
                    vm.stack.date_fixed = !isFixed() ? moment().toDate() : null;
                    if (isRegressed() && isFixed())
                        vm.stack.is_regressed = false;
                }

                function onFailure() {
                    var action = isFixed() ? ' not' : '';
                    notificationService.error('An error occurred while marking this stack as' + action + ' fixed.');
                }

                if (isFixed()) {
                    return stackService.markNotFixed(stackId).then(onSuccess, onFailure);
                }

                return stackService.markFixed(stackId).then(onSuccess, onFailure);
            }

            function updateIsHidden() {
                function onSuccess() {
                    vm.stack.is_hidden = !isHidden();
                }

                function onFailure() {
                    notificationService.error('An error occurred while marking this stack as ' + isHidden() ? 'shown.' : 'hidden.');
                }

                if (isHidden()) {
                    return stackService.markNotHidden(stackId).then(onSuccess, onFailure);
                }

                return stackService.markHidden(stackId).then(onSuccess, onFailure);
            }

            function updateNotifications() {
                function onSuccess() {
                    vm.stack.disable_notifications = !notificationsDisabled();
                }

                function onFailure() {
                    var action = notificationsDisabled() ? 'enabling' : 'disabling';
                    notificationService.error('An error occurred while ' + action + ' stack notifications.');
                }

                if (notificationsDisabled()) {
                    return stackService.enableNotifications(stackId).then(onSuccess, onFailure);
                }

                return stackService.disableNotifications(stackId).then(onSuccess, onFailure);
            }

            vm.addReferenceLink = addReferenceLink;
            vm.hasTags = hasTags;
            vm.hasReference = hasReference;
            vm.hasReferences = hasReferences;
            vm.isCritical = isCritical;
            vm.isFixed = isFixed;
            vm.isHidden = isHidden;
            vm.isRegressed = isRegressed;
            vm.notificationsDisabled = notificationsDisabled;
            vm.promoteToExternal = promoteToExternal;
            vm.removeReferenceLink = removeReferenceLink;
            vm.recentOccurrences = {
                get: function (options) {
                    return eventService.getByStackId(stackId, options);
                },
                options: {
                    limit: 4,
                    mode: 'summary'
                }
            };
            vm.resetOccurrences = resetOccurrences;
            vm.stack = {};
            vm.updateIsCritical = updateIsCritical;
            vm.updateIsFixed = updateIsFixed;
            vm.updateIsHidden = updateIsHidden;
            vm.updateNotifications = updateNotifications;

            get();
        }]);
}());
