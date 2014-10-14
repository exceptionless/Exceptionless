(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Add', ['organizationService', function (organizationService) {
            var vm = this;

            function hasOrganizations() {
                return vm.organizations.length > 0;
            }

            vm.hasOrganizations = hasOrganizations;
            vm.organizations = [];
        }
    ]);
}());
