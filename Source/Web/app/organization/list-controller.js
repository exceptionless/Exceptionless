(function () {
    'use strict';

    angular.module('app.organization')
        .controller('organization.List', ['$rootScope', '$scope', '$window', '$state', 'dialogService', 'linkService', 'notificationService', 'organizationService', function ($rootScope, $scope, $window, $state, dialogService, linkService, notificationService, organizationService) {
            var options = { limit: 10, mode: 'summary' };
            var vm = this;

            console.log(this);

            function add() {

            }

            function get() {
                organizationService.getAll(options).then(function (response) {
                    vm.organizations = response.data.plain();

                    var links = linkService.getLinksQueryParameters(response.headers('link'));
                    vm.previous = links['previous'];
                    vm.next = links['next'];
                });
            }

            function leave(organization) {

            }

            function open(id, event) {
                // TODO: implement this.
                if (event.ctrlKey || event.which === 2) {
                    $window.open('/#/organization/' + id + '/manage/', '_blank');
                } else {
                    $state.go('app.organization.manage', { id: id });
                }
            }

            function nextPage() {
                get(vm.next);
            }

            function previousPage() {
                get(vm.previous);
            }

            function remove(organization) {
                return dialogService.confirmDanger('Are you sure you want to remove the organization?', 'REMOVE ORGANIZATION').then(function() {
                    function onSuccess() {
                        vm.organizations.splice(vm.organizations.indexOf(organization), 1);
                    }

                    function onFailure() {
                        notificationService.error('An error occurred while trying to remove the organization.');
                    }

                    return organizationService.remove(organization.id).then(onSuccess, onFailure);
                });
            }

            var unbind = $rootScope.$on('OrganizationChanged', function(e, data){
                if ($scope.previous === undefined)
                    get($scope.settings.options);
            });

            $scope.$on('$destroy', unbind);

            vm.add = add;
            vm.leave = leave;
            vm.nextPage = nextPage;
            vm.open = open;
            vm.organizations = [];
            vm.previousPage = previousPage;
            vm.remove = remove;

            get();
        }
    ]);
}());
