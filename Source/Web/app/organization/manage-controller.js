(function () {
    'use strict';

    angular.module('app.organization')
        .controller('organization.Manage', ['$state', '$stateParams', 'organizationService', 'projectService', 'userService', 'notificationService', 'featureService', 'dialogs', 'dialogService', 'debounce', function ($state, $stateParams, organizationService, projectService, userService, notificationService, featureService, dialogs, dialogService, debounce) {
            var organizationId = $stateParams.id;
            var options = { limit: 5 };
            var vm = this;

            function addUser() {

            }

            function get() {
                return organizationService.getById(organizationId)
                    .then(function (response) {
                        vm.organization = response.data;
                    }, function() {
                        $state.go('app.dashboard');
                        notificationService.error('The organization "' + $stateParams.id + '" could not be found.');
                    });
            }

            function getInvoices() {
                return organizationService.getInvoices(organizationId, options)
                    .then(function (response) {
                        vm.invoices = response.data;
                    }, function() {
                        notificationService.error('The invoices for this organization could not be loaded.');
                    });
            }

            function getUsers() {
                return userService.getByOrganizationId(organizationId, options)
                    .then(function (response) {
                        vm.users = response.data;
                    }, function() {
                        notificationService.error('The users for this organization could not be loaded.');
                    });
            }

            function hasInvoices() {
                return vm.invoices.length > 0;
            }

            function hasPremiumFeatures(){
                return featureService.hasPremium();
            }

            function removeUser() {

            }

            function resendNotification() {

            }

            function open(id, event) {
                if (event.ctrlKey || event.which === 2) {
                    $window.open('/#/organization/payment/' + id, '_blank');
                } else {
                    $state.go('app.organization.payment', { id: id });
                }
            }

            function save(isValid) {
                if (!isValid){
                    return;
                }

                function onFailure() {
                    notificationService.error('An error occurred while saving the organization.');
                }

                return organizationService.update(organizationId, vm.organization).catch(onFailure);
            }

            vm.addUser = addUser;
            vm.hasInvoices = hasInvoices;
            vm.hasPremiumFeatures = hasPremiumFeatures;
            vm.invoices = [];
            vm.open = open;
            vm.organization = {};
            vm.projects = {
                get: function (options) {
                    return projectService.getByOrganizationId(organizationId, options);
                },
                options: {
                    limit: 10,
                    mode: 'summary'
                }
            };
            vm.removeUser = removeUser;
            vm.resendNotification = resendNotification;
            vm.save = debounce(save, 1000);
            vm.users = [];

            get().then(getUsers).then(getInvoices);
        }
    ]);
}());
