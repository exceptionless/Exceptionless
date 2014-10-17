(function () {
    'use strict';

    angular.module('app.account')
        .controller('account.Manage', ['projectService', 'notificationService', 'featureService', 'dialogs', 'dialogService', 'debounce', function (projectService, notificationService, featureService, dialogs, dialogService, debounce) {
            var vm = this;

            function changePassword(isValid) {

            }

            function getProjects() {
                return projectService.getById(projectId)
                    .then(function (response) {
                        vm.project = response.data;
                    }, function() {
                        $state.go('app.dashboard');
                        notificationService.error('The project "' + $stateParams.id + '" could not be found.');
                    });
            }

            function hasPremiumFeatures(){
                return featureService.hasPremium();
            }


            function isVerified() {
                return false;
            }

            function resendVerificationEmail() {

            }

            function save(isValid) {
                if (!isValid){
                    return;
                }

                function onFailure() {
                    notificationService.error('An error occurred while saving the project.');
                }

                return projectService.update(vm.project.id, vm.project).catch(onFailure);
            }

            function saveNotificationSettings() {

            }

            vm.changePassword = changePassword;
            vm.hasPremiumFeatures = hasPremiumFeatures;
            vm.isVerified = isVerified;
            vm.password = {};
            vm.profile = {};
            vm.project = {};
            vm.resendVerificationEmail = resendVerificationEmail;
            vm.save = debounce(save, 1000);
            vm.saveNotificationSettings = saveNotificationSettings;
        }
    ]);
}());
