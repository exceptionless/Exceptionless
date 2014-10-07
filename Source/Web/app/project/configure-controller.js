(function () {
    'use strict';

    angular.module('app.project')
        .controller('Configure', ['projectService', function (projectService) {
            function getProjectTypes() {
                return [
                    { key: 'Exceptionless.Mvc', name: 'ASP.NET MVC', config: 'web.config' },
                    { key: 'Exceptionless.WebApi', name: 'ASP.NET Web API', config: 'web.config' },
                    { key: 'Exceptionless.Web', name: 'ASP.NET Web Forms', config: 'web.config' },
                    { key: 'Exceptionless.Windows', name: 'Windows Forms', config: 'app.config' },
                    { key: 'Exceptionless.Wpf', name: 'Windows Presentation Foundation (WPF)', config: 'app.config' },
                    { key: 'Exceptionless.Nancy', name: 'Nancy', config: 'app.config' },
                    { key: 'Exceptionless', name: 'Console', config: 'app.config' }
                ];
            }

            var vm = this;
            vm.currentProjectType = null;
            vm.projectTypes = getProjectTypes();
        }
    ]);
}());
