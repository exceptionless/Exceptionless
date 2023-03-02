(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.Configure', function ($rootScope, $state, $stateParams, BASE_URL, EXCEPTIONLESS_SERVER_URL, notificationService, projectService, tokenService, translateService) {
      var vm = this;
      function canRedirect(data) {
        return vm._canRedirect && !!data && data.project_id === vm._projectId;
      }

      function copied() {
        notificationService.success(translateService.T('Copied!'));
      }

      function onCopyError() {
        function getCopyTooltip() {
          if (/iPhone|iPad/i.test(navigator.userAgent)) {
            return translateService.T('Copy not supported.');
          } else if (/Mac/i.test(navigator.userAgent)) {
            return translateService.T('Press ⌘-C to copy.');
          } else {
            return translateService.T('Press Ctrl-C to copy.');
          }
        }

        var element = $('input.api-key');
        element.tooltip({ placement: 'bottom', title: getCopyTooltip() });
        element.select();
      }

      function getDefaultApiKey() {
        function onSuccess(response) {
          vm.apiKey = response.data.id;
          return vm.apiKey;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while getting the API key for your project.'));
        }

        return tokenService.getProjectDefault(vm._projectId).then(onSuccess, onFailure);
      }

      function getProject() {
        function onSuccess(response) {
          vm.project = response.data.plain();
          vm.projectName = vm.project.name ? ('"' + vm.project.name + '"') : '';
          return vm.project;
        }

        function onFailure() {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Project', { projectId : vm._projectId }));
        }

        return projectService.getById(vm._projectId, true).then(onSuccess, onFailure);
      }

      function getProjectTypes() {
        return [
          { key: 'Bash Shell', name: 'Bash Shell', platform: 'Command Line' },
          { key: 'PowerShell', name: 'PowerShell', platform: 'Command Line' },
          { key: 'Exceptionless', name: translateService.T('Console and Service applications'), platform: '.NET' },
          { key: 'Exceptionless.AspNetCore', name: 'ASP.NET Core', platform: '.NET' },
          { key: 'Exceptionless.Mvc', name: 'ASP.NET MVC', config: 'web.config', platform: '.NET' },
          { key: 'Exceptionless.WebApi', name: 'ASP.NET Web API', config: 'web.config', platform: '.NET' },
          { key: 'Exceptionless.Web', name: 'ASP.NET Web Forms', config: 'web.config', platform: '.NET' },
          { key: 'Exceptionless.Windows', name: 'Windows Forms', config: 'app.config', platform: '.NET' },
          { key: 'Exceptionless.Wpf', name: 'Windows Presentation Foundation (WPF)', config: 'app.config', platform: '.NET' },
          { key: 'Exceptionless.Nancy', name: 'Nancy', config: 'app.config', platform: '.NET' },
          { key: 'Exceptionless.JavaScript', name: translateService.T('Browser applications'), platform: 'JavaScript' },
          { key: 'Exceptionless.Node', name: 'Node.js', platform: 'JavaScript' }
        ];
      }

      function isCommandLine() {
        return vm.currentProjectType.platform === 'Command Line';
      }

      function isDotNet() {
       return vm.currentProjectType.platform === '.NET';
      }

      function isJavaScript() {
        return vm.currentProjectType.platform === 'JavaScript';
      }

      function isNode() {
        return vm.currentProjectType.key === 'Exceptionless.Node';
      }

      function isBashShell() {
        return vm.currentProjectType.key === 'Bash Shell';
      }

      function navigateToDashboard() {
        $state.go('app.project-frequent', { projectId: vm._projectId } );
      }

      this.$onInit = function $onInit() {
        vm._projectId = $stateParams.id;
        vm._canRedirect = $stateParams.redirect === 'true';
        vm.apiKey = null;
        vm.canRedirect = canRedirect;
        vm.copied = copied;
        vm.currentProjectType = {};
        vm.isBashShell = isBashShell;
        vm.isCommandLine = isCommandLine;
        vm.isDotNet = isDotNet;
        vm.isJavaScript = isJavaScript;
        vm.isNode = isNode;
        vm.navigateToDashboard = navigateToDashboard;
        vm.onCopyError = onCopyError;
        vm.project = {};
        vm.projectTypes = getProjectTypes();
        vm.serverUrl = EXCEPTIONLESS_SERVER_URL || BASE_URL;

        getDefaultApiKey().then(getProject);
      };
    });
}());
