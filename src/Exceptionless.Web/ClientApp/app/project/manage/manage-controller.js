/* global Rickshaw:false */
(function () {
  'use strict';

  angular.module('app.project')
    .controller('project.Manage', function ($ExceptionlessClient, $filter, $state, $stateParams, billingService, filterService, organizationService, projectService, tokenService, webHookService, notificationService, STRIPE_PUBLISHABLE_KEY, dialogs, dialogService, translateService) {
      var vm = this;
      function addConfiguration() {
        return dialogs.create('app/project/manage/add-configuration-dialog.tpl.html', 'AddConfigurationDialog as vm', vm.config).result.then(saveClientConfiguration).catch(function(e){});
      }

      function addSlack() {
        if (!vm.hasPremiumFeatures) {
          return billingService.confirmUpgradePlan(translateService.T("Please upgrade your plan to enable slack integration."), vm.project.organization_id).then(function () {
            return addSlackIntegration();
          }).catch(function(e){});
        }

        return addSlackIntegration();
      }

      function addSlackIntegration() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while adding Slack to your project.'));
        }

        return projectService.addSlack(vm._projectId).catch(onFailure);
      }

      function addToken() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while creating a new API key for your project.'));
        }

        var options = {organization_id: vm.project.organization_id, project_id: vm._projectId};
        return tokenService.create(options).catch(onFailure);
      }

      function addWebHook() {
        return dialogs.create('components/web-hook/add-web-hook-dialog.tpl.html', 'AddWebHookDialog as vm').result.then(function (data) {
          data.project_id = vm._projectId;
          return createWebHook(data);
        }).catch(function(e){});
      }

      function changePlan() {
        return billingService.changePlan(vm.organization.id).catch(function(e){});
      }

      function createWebHook(data) {
        function onFailure(response) {
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message, vm.project.organization_id).then(function () {
              return createWebHook(data);
            }).catch(function(e){});
          }

          notificationService.error(translateService.T('An error occurred while saving the configuration setting.'));
        }

        return webHookService.create(data).catch(onFailure);
      }

      function copied() {
        notificationService.success(translateService.T('Copied!'));
      }

      function enable(token) {
        return dialogService.confirm(translateService.T('Are you sure you want to enable the API key?'), translateService.T('ENABLE API KEY')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while enabling the API key.'));
          }

          return tokenService.update(token.id, { is_disabled: false }).catch(onFailure);
        }).catch(function(e){});
      }

      function disable(token) {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to disable the API key?'), translateService.T('DISABLE API KEY')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while disabling the API key.'));
          }

          return tokenService.update(token.id, { is_disabled: true }).catch(onFailure);
        }).catch(function(e){});
      }

      function get(data) {
        if (vm._ignoreRefresh) {
          return;
        }

        if (data && data.type === 'Project' && data.deleted && data.id === vm._projectId) {
          $state.go('app.project.list');
          notificationService.error(translateService.T('Project_Deleted', {projectId: vm._projectId}));
          return;
        }

        return getProject().then(getOrganization).then(getConfiguration).then(getTokens).then(getSlackNotificationSettings).then(getWebHooks);
      }

      function getOrganization() {
        function onSuccess(response) {
          function getRemainingEventLimit(organization) {
            if (!organization.max_events_per_month) {
              return 0;
            }

            var bonusEvents = moment.utc().isBefore(moment.utc(organization.bonus_expiration)) ? organization.bonus_events_per_month : 0;
            var usage = organization.usage && organization.usage[vm.organization.usage.length - 1];
            if (usage && moment.utc(usage.date).isSame(moment.utc().startOf('month'))) {
              var remaining = usage.limit - (usage.total - usage.blocked);
              return remaining > 0 ? remaining : 0;
            }

            return organization.max_events_per_month + bonusEvents;
          }

          vm.organization = response.data.plain();
          vm.hasMonthlyUsage = vm.organization.max_events_per_month > 0;
          vm.remainingEventLimit = getRemainingEventLimit(vm.organization);
          vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organization;

          vm.project.usage = vm.project.usage || [];
          vm.organization.usage = (vm.organization.usage || [{ date: moment.utc().startOf('month').toISOString(), total: 0, blocked: 0, limit: vm.organization.max_events_per_month, too_big: 0 }]).filter(function (usage) {
            return vm.project.usage.some(function(u) { return moment(u.date).isSame(usage.date); });
          });


          vm.chart.options.series[0].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.total - item.blocked - item.too_big, data: item};
          });

          vm.chart.options.series[1].data = vm.project.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.total - item.blocked - item.too_big, data: item};
          });

          vm.chart.options.series[2].data = vm.project.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.blocked, data: item};
          });

          vm.chart.options.series[3].data = vm.project.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.too_big, data: item};
          });

          vm.chart.options.series[4].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.limit, data: item};
          });

          return vm.organization;
        }

        function onFailure() {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Organization',{organizationId: vm.project.organization_id}));
        }

        return organizationService.getById(vm.project.organization_id, false).then(onSuccess, onFailure);
      }

      function getProject() {
        function onSuccess(response) {
          vm.common_methods = null;
          vm.user_namespaces = null;

          vm.project = response.data.plain();
          vm.hasPremiumFeatures = vm.project.has_premium_features;
          if (vm.project && vm.project.data) {
            vm.common_methods = vm.project.data['CommonMethods'];
            vm.user_namespaces = vm.project.data['UserNamespaces'];
          }

          vm.project.usage = vm.project.usage || [{ date: moment.utc().startOf('month').toISOString(), total: 0, blocked: 0, limit: 3000, too_big: 0 }];
          return vm.project;
        }

        function onFailure() {
          $state.go('app.project.list');
          notificationService.error(translateService.T('Cannot_Find_Project',{projectId: vm._projectId}));
        }

        return projectService.getById(vm._projectId).then(onSuccess, onFailure);
      }

      function getTokens() {
        function onSuccess(response) {
          vm.tokens = response.data.plain();
          return vm.tokens;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred loading the api keys.'));
        }

        return tokenService.getByProjectId(vm._projectId).then(onSuccess, onFailure);
      }

      function getConfiguration() {
        function onSuccess(response) {
          vm.config = [];
          vm.data_exclusions = null;
          vm.exclude_private_information = false;
          vm.user_agents = null;

          angular.forEach(response.data.settings, function (value, key) {
            if (key === '@@DataExclusions') {
              vm.data_exclusions = value;
            } else if (key === '@@IncludePrivateInformation') {
              vm.exclude_private_information = value === "false";
            } else if (key === '@@UserAgentBotPatterns') {
              vm.user_agents = value;
            } else {
              vm.config.push({key: key, value: value});
            }
          });

          return vm.config;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred loading the notification settings.'));
        }

        return projectService.getConfig(vm._projectId).then(onSuccess, onFailure);
      }

      function getSlackNotificationSettings() {
        function onSuccess(response) {
          vm.slackNotificationSettings = response.data.plain();
          return vm.slackNotificationSettings;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while loading the slack notification settings.'));
        }

        vm.slackNotificationSettings = null;
        return projectService.getIntegrationNotificationSettings(vm._projectId, 'slack').then(onSuccess, onFailure);
      }

      function getWebHooks() {
        function onSuccess(response) {
          vm.webHooks = response.data.plain();
          return vm.webHooks;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred loading the notification settings.'));
        }

        return webHookService.getByProjectId(vm._projectId).then(onSuccess, onFailure);
      }

      function removeConfig(config) {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this configuration setting?'), translateService.T('DELETE CONFIGURATION SETTING')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to delete the configuration setting.'));
          }

          return projectService.removeConfig(vm._projectId, config.key).catch(onFailure);
        }).catch(function(e){});
      }

      function removeProject() {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete the "' + vm.project.name + '" project?'), translateService.T('Delete Project')).then(function () {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully queued the project for deletion.'));
            $state.go('app.project.list');
          }

          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to delete the project.'));
            vm._ignoreRefresh = false;
          }

          vm._ignoreRefresh = true;
          return projectService.remove(vm._projectId).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function removeSlack() {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to remove slack support?'), translateService.T('Remove Slack')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to remove slack.'));
          }

          return projectService.removeSlack(vm._projectId).catch(onFailure);
        }).catch(function(e){});
      }

      function removeToken(token) {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this API key?'), translateService.T('DELETE API KEY')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to delete the API Key.'));
          }

          return tokenService.remove(token.id).catch(onFailure);
        }).catch(function(e){});
      }

      function removeWebHook(hook) {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this web hook?'), translateService.T('DELETE WEB HOOK')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to delete the web hook.'));
          }

          return webHookService.remove(hook.id).catch(onFailure);
        }).catch(function(e){});
      }

      function resetData() {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to reset the data for this project?'), translateService.T('RESET PROJECT DATA')).then(function () {
          function onFailure() {
            notificationService.error(translateService.T('An error occurred while resetting project data.'));
          }

          return projectService.resetData(vm._projectId).catch(onFailure);
        }).catch(function(e){});
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the project.'));
        }

        return projectService.update(vm._projectId, vm.project).catch(onFailure);
      }

      function saveApiKeyNote(data) {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the API key note.'));
        }

        return tokenService.update(data.id, { notes: data.notes }).catch(onFailure);
      }

      function saveClientConfiguration(data) {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the configuration setting.'));
        }

        return projectService.setConfig(vm._projectId, data.key, data.value).catch(onFailure);
      }

      function saveCommonMethods() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the common methods.'));
        }

        if (vm.common_methods) {
          return projectService.setData(vm._projectId, 'CommonMethods', vm.common_methods).catch(onFailure);
        } else {
          return projectService.removeData(vm._projectId, 'CommonMethods').catch(onFailure);
        }
      }

      function saveDataExclusion() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the data exclusion.'));
        }

        if (vm.data_exclusions) {
          return projectService.setConfig(vm._projectId, '@@DataExclusions', vm.data_exclusions).catch(onFailure);
        } else {
          return projectService.removeConfig(vm._projectId, '@@DataExclusions').catch(onFailure);
        }
      }

      function saveDeleteBotDataEnabled() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the project.'));
        }

        return projectService.update(vm._projectId, {'delete_bot_data_enabled': vm.project.delete_bot_data_enabled}).catch(onFailure);
      }

      function saveIncludePrivateInformation() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the include private information setting.'));
        }

        if (vm.exclude_private_information === true) {
          return projectService.setConfig(vm._projectId, '@@IncludePrivateInformation', false).catch(onFailure);
        } else {
          return projectService.removeConfig(vm._projectId, '@@IncludePrivateInformation').catch(onFailure);
        }
      }

      function saveUserAgents() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the user agents.'));
        }

        if (vm.user_agents) {
          return projectService.setConfig(vm._projectId, '@@UserAgentBotPatterns', vm.user_agents).catch(onFailure);
        } else {
          return projectService.removeConfig(vm._projectId, '@@UserAgentBotPatterns').catch(onFailure);
        }
      }

      function saveUserNamespaces() {
        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the user namespaces.'));
        }

        if (vm.user_namespaces) {
          return projectService.setData(vm._projectId, 'UserNamespaces', vm.user_namespaces).catch(onFailure);
        } else {
          return projectService.removeData(vm._projectId, 'UserNamespaces').catch(onFailure);
        }
      }

      function saveSlackNotificationSettings() {
        function onFailure(response) {
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message, vm.project.organization_id).then(function () {
              return saveSlackNotificationSettings();
            }).catch(function(e){
              return getSlackNotificationSettings();
            });
          }

          notificationService.error(translateService.T('An error occurred while saving your slack notification settings.'));
        }

        return projectService.setIntegrationNotificationSettings(vm._projectId, 'slack', vm.slackNotificationSettings).catch(onFailure);
      }

      function showChangePlanDialog() {
        return billingService.changePlan(vm.project.organization_id).catch(function(e){});
      }

      function validateApiKeyNote(original, data) {
        if (original === data) {
          return false;
        }

        return null;
      }

      function validateClientConfiguration(original, data) {
        if (original === data) {
          return false;
        }

        return !data ? translateService.T('Please enter a valid value.') : null;
      }

      this.$onInit = function $onInit() {
        vm._ignoreRefresh = false;
        vm._projectId = $stateParams.id;
        vm.addSlack = addSlack;
        vm.addToken = addToken;
        vm.addConfiguration = addConfiguration;
        vm.addWebHook = addWebHook;
        vm.canChangePlan = false;
        vm.changePlan = changePlan;
        vm.chart = {
          options: {
            padding: {top: 0.085},
            renderer: 'multi',
            series: [{
              name: translateService.T('Allowed in Organization'),
              color: '#f5f5f5',
              renderer: 'area'
            },
            {
              name: translateService.T('Allowed'),
              color: '#a4d56f',
              renderer: 'stack'
            }, {
              name: translateService.T('Blocked'),
              color: '#e2e2e2',
              renderer: 'stack'
            }, {
              name: translateService.T('Too Big'),
              color: '#ccc',
              renderer: 'stack'
            }, {
              name: translateService.T('Limit'),
              color: '#a94442',
              renderer: 'dotted_line'
            }]
          },
          features: {
            hover: {
              render: function (args) {
                var date = moment.utc(args.domainX, 'X');
                var dateTimeFormat = translateService.T('DateTimeFormat');
                var dateFormat = translateService.T('DateFormat');
                var formattedDate = date.hours() === 0 && date.minutes() === 0 ? date.format(dateFormat || 'ddd, MMM D, YYYY') : date.format(dateTimeFormat || 'ddd, MMM D, YYYY h:mma');
                var content = '<div class="date">' + formattedDate + '</div>';
                args.detail.sort(function (a, b) {
                  return a.order - b.order;
                }).forEach(function (d) {
                  var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                  content += swatch + $filter('number')(d.formattedYValue) + ' ' + d.series.name + '<br />';
                }, this);

                content += '<span class="detail-swatch"></span>' + $filter('number')(args.detail[1].value.data.total) + ' Total<br />';

                var xLabel = document.createElement('div');
                xLabel.className = 'x_label';
                xLabel.innerHTML = content;
                this.element.appendChild(xLabel);

                // If left-alignment results in any error, try right-alignment.
                var leftAlignError = this._calcLayoutError([xLabel]);
                if (leftAlignError > 0) {
                  xLabel.classList.remove('left');
                  xLabel.classList.add('right');

                  // If right-alignment is worse than left alignment, switch back.
                  var rightAlignError = this._calcLayoutError([xLabel]);
                  if (rightAlignError > leftAlignError) {
                    xLabel.classList.remove('right');
                    xLabel.classList.add('left');
                  }
                }

                this.show();
              }
            },
            range: {
              onSelection: function (position) {
                var start = moment.unix(position.coordMinX).utc().local();
                var end = moment.unix(position.coordMaxX).utc().local();

                filterService.setTime(start.format('YYYY-MM-DDTHH:mm:ss') + '-' + end.format('YYYY-MM-DDTHH:mm:ss'));
                $ExceptionlessClient.createFeatureUsage(vm._source + '.chart.range.onSelection')
                  .setProperty('start', start)
                  .setProperty('end', end)
                  .submit();

                $state.go('app.project-frequent', { projectId: vm.project.id });
                return false;
              }
            },
            xAxis: {
              timeFixture: new Rickshaw.Fixtures.Time.Local(),
              overrideTimeFixtureCustomFormatters: true
            },
            yAxis: {
              ticks: 5,
              tickFormat: 'formatKMBT',
              ticksTreatment: 'glow'
            }
          }
        };
        vm.config = [];
        vm.copied = copied;
        vm.common_methods = null;
        vm.data_exclusions = null;
        vm.disable = disable;
        vm.enable = enable;
        vm.get = get;
        vm.getOrganization = getOrganization;
        vm.getTokens = getTokens;
        vm.getWebHooks = getWebHooks;
        vm.getSlackNotificationSettings = getSlackNotificationSettings;
        vm.hasMonthlyUsage = true;
        vm.hasPremiumFeatures = false;
        vm.exclude_private_information = false;
        vm.next_billing_date = moment().startOf('month').add(1, 'months').toDate();
        vm.organization = {};
        vm.project = {};
        vm.projectForm = {};
        vm.remainingEventLimit = 3000;
        vm.removeConfig = removeConfig;
        vm.removeProject = removeProject;
        vm.removeSlack = removeSlack;
        vm.removeToken = removeToken;
        vm.removeWebHook = removeWebHook;
        vm.resetData = resetData;
        vm.save = save;
        vm.saveApiKeyNote = saveApiKeyNote;
        vm.saveClientConfiguration = saveClientConfiguration;
        vm.saveCommonMethods = saveCommonMethods;
        vm.saveDataExclusion = saveDataExclusion;
        vm.saveDeleteBotDataEnabled = saveDeleteBotDataEnabled;
        vm.saveIncludePrivateInformation = saveIncludePrivateInformation;
        vm.saveSlackNotificationSettings = saveSlackNotificationSettings;
        vm.saveUserAgents = saveUserAgents;
        vm.saveUserNamespaces = saveUserNamespaces;
        vm.showChangePlanDialog = showChangePlanDialog;
        vm.slackNotificationSettings = null;
        vm.tokens = [];
        vm.user_agents = null;
        vm.user_namespaces = null;
        vm.validateApiKeyNote = validateApiKeyNote;
        vm.validateClientConfiguration = validateClientConfiguration;
        vm.webHooks = [];
        get();
      };
    });
}());
