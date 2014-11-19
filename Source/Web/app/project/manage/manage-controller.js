(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.Manage', ['$state', '$stateParams', 'projectService', 'tokenService', 'webHookService', 'notificationService', 'featureService', 'dialogs', 'dialogService', function ($state, $stateParams, projectService, tokenService, webHookService, notificationService, featureService, dialogs, dialogService) {
      var projectId = $stateParams.id;
      var vm = this;

      function addConfiguration() {
        dialogs.create('app/project/manage/add-configuration-dialog.tpl.html', 'AddConfigurationDialog as vm').result.then(function (data) {
          function onSuccess() {
            var found = false;
            vm.config.forEach(function (conf) {
              if (conf.key === data.key) {
                found = true;
                conf.value = data.value;
              }
            });

            if (!found) {
              vm.config.push(data);
            }
          }

          function onFailure() {
            notificationService.error('An error occurred while saving the configuration setting.');
          }

          return projectService.setConfig(projectId, data.key, data.value).then(onSuccess, onFailure);
        });
      }

      function addToken() {
        function onSuccess(response) {
          vm.tokens.push(response.data.plain());
        }

        function onFailure() {
          notificationService.error('An error occurred while creating a new API key for your project.');
        }

        var options = {organization_id: vm.project.organization_id, project_id: projectId};
        return tokenService.create(options).then(onSuccess, onFailure);
      }

      function addWebHook() {
        dialogs.create('components/web-hook/add-web-hook-dialog.tpl.html', 'AddWebHookDialog as vm').result.then(function (data) {
          function onSuccess(response) {
            vm.webHooks.push(response.data);
            return response.data.plain();
          }

          function onFailure() {
            notificationService.error('An error occurred while saving the configuration setting.');
          }

          data.organization_id = vm.project.organization_id;
          data.project_id = projectId;
          return webHookService.create(data).then(onSuccess, onFailure);
        });
      }

      function get() {
        function onSuccess(response) {
          vm.project = response.plain();
          return vm.project;
        }

        function onFailure() {
          $state.go('app.dashboard');
          notificationService.error('The project "' + $stateParams.id + '" could not be found.');
        }

        return projectService.getById(projectId).then(onSuccess, onFailure);
      }

      function getTokens() {
        function onSuccess(response) {
          vm.tokens = response.data.plain();
          return vm.tokens;
        }

        function onFailure() {
          notificationService.error('An error occurred loading the api keys.');
        }

        return tokenService.getByProjectId(projectId).then(onSuccess, onFailure);
      }

      function getConfiguration() {
        function onSuccess(response) {
          angular.forEach(response.data.settings, function (value, key) {
            if (key === '@@DataExclusions') {
              vm.data_exclusions = value;
            } else {
              vm.config.push({key: key, value: value});
            }
          });

          return vm.config;
        }

        function onFailure() {
          notificationService.error('An error occurred loading the notification settings.');
        }

        return projectService.getConfig(projectId).then(onSuccess, onFailure);
      }

      function getWebHooks() {
        function onSuccess(response) {
          vm.webHooks = response.data.plain();
          return vm.webHooks;
        }

        function onFailure() {
          notificationService.error('An error occurred loading the notification settings.');
        }

        return webHookService.getByProjectId(projectId).then(onSuccess, onFailure);
      }

      function hasConfiguration() {
        return vm.config.length > 0;
      }

      function hasPremiumFeatures() {
        return featureService.hasPremium();
      }

      function hasTokens() {
        return vm.tokens.length > 0;
      }

      function hasWebHook() {
        return vm.webHooks.length > 0;
      }

      function removeToken(token) {
        return dialogService.confirmDanger('Are you sure you want to remove the API key?', 'REMOVE API KEY').then(function () {
          function onSuccess() {
            vm.tokens.splice(vm.tokens.indexOf(token), 1);
          }

          function onFailure() {
            notificationService.error('An error occurred while trying to remove the API Key.');
          }

          return tokenService.remove(token.id).then(onSuccess, onFailure);
        });
      }

      function removeConfig(config) {
        return dialogService.confirmDanger('Are you sure you want to remove this configuration setting?', 'REMOVE CONFIGURATION SETTING').then(function () {
          function onSuccess() {
            vm.config.splice(vm.config.indexOf(config), 1);
          }

          function onFailure() {
            notificationService.error('An error occurred while trying to remove the configuration setting.');
          }

          return projectService.removeConfig(projectId, config.key).then(onSuccess, onFailure);
        });
      }

      function removeWebHook(hook) {

      }

      function resetData() {
        return dialogService.confirmDanger('Are you sure you want to reset the data for this project?', 'RESET PROJECT DATA').then(function () {
          function onFailure() {
            notificationService.error('An error occurred while resetting project data.');
          }

          return projectService.resetData(projectId).catch(onFailure);
        });
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure() {
          notificationService.error('An error occurred while saving the project.');
        }

        return projectService.update(projectId, vm.project).catch(onFailure);
      }

      function saveDataExclusion() {
        function onFailure() {
          notificationService.error('An error occurred while saving the the data exclusion.');
        }

        if (vm.data_exclusions) {
          return projectService.setConfig(projectId, '@@DataExclusions', vm.data_exclusions).catch(onFailure);
        } else {
          return projectService.removeConfig(projectId, '@@DataExclusions').catch(onFailure);
        }
      }

      function saveDeleteBotDataEnabled() {
        function onFailure() {
          notificationService.error('An error occurred while saving the project.');
        }

        return projectService.update(projectId, {'delete_bot_data_enabled': vm.project.delete_bot_data_enabled}).catch(onFailure);
      }

      vm.addToken = addToken;
      vm.addConfiguration = addConfiguration;
      vm.addWebHook = addWebHook;
      vm.config = [];
      vm.data_exclusions = null;
      vm.hasConfiguration = hasConfiguration;
      vm.hasPremiumFeatures = hasPremiumFeatures;
      vm.hasTokens = hasTokens;
      vm.hasWebHook = hasWebHook;
      vm.project = {};
      vm.removeConfig = removeConfig;
      vm.removeToken = removeToken;
      vm.removeWebHook = removeWebHook;
      vm.resetData = resetData;
      vm.save = save;
      vm.saveDataExclusion = saveDataExclusion;
      vm.saveDeleteBotDataEnabled = saveDeleteBotDataEnabled;
      vm.tokens = [];
      vm.webHooks = [];

      get().then(getTokens).then(getConfiguration).then(getWebHooks);
    }
    ]);
}());
