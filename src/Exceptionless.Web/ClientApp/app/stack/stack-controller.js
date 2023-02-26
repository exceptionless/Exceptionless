/* global Rickshaw:false */
(function () {
  'use strict';

  angular.module('app.stack')
    .controller('Stack', function ($scope, $ExceptionlessClient, $filter, hotkeys, $state, $stateParams, billingService, dialogs, dialogService, eventService, filterService, notificationService, organizationService, projectService, stackDialogService, stackService, translateService) {
      var vm = this;
      function addHotkeys() {
        function logFeatureUsage(name) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.hotkeys' + name).addTags('hotkeys').submit();
        }

        hotkeys.del('shift+h');
        hotkeys.del('shift+f');
        hotkeys.del('shift+c');
        hotkeys.del('shift+m');
        hotkeys.del('shift+p');
        hotkeys.del('shift+r');
        hotkeys.del('shift+Fbackspace');

        hotkeys.bindTo($scope)
          .add({
            combo: 'shift+h',
            description: translateService.T(vm.stack.status === "discarded" ? 'Mark Stack Open' : 'Mark Stack Discarded'),
            callback: function markIgnored() {
              logFeatureUsage('Ignored');
              vm.updateIgnored();
            }
          })
          .add({
            combo: 'shift+f',
            description: translateService.T(vm.stack.status === 'fixed' ? 'Mark Stack Open' : 'Mark Stack Fixed'),
            callback: function markFixed() {
              logFeatureUsage('Fixed');
              vm.updateIsFixed();
            }
          })
          .add({
            combo: 'shift+c',
            description: translateService.T(vm.stack.occurrences_are_critical ? 'Future Stack Occurrences are Not Critical' : 'Future Stack Occurrences are Critical'),
            callback: function markCritical() {
              logFeatureUsage('Critical');
              vm.updateIsCritical();
            }
          })
          .add({
            combo: 'shift+m',
            description: translateService.T(vm.stack.disable_notifications ? 'Enable Stack Notifications' : 'Disable Stack Notifications'),
            callback: function updateNotifications() {
              logFeatureUsage('Notifications');
              vm.updateNotifications();
            }
          })
          .add({
            combo: 'shift+p',
            description: translateService.T('Promote Stack To External'),
            callback: function promote() {
              logFeatureUsage('Promote');
              vm.promoteToExternal();
            }
          })
          .add({
            combo: 'shift+r',
            description: translateService.T('Add Stack Reference Link'),
            callback: function addReferenceLink() {
              logFeatureUsage('Reference');
              vm.addReferenceLink();
            }
          })
          .add({
            combo: 'shift+backspace',
            description: translateService.T('Delete Stack'),
            callback: function deleteStack() {
              logFeatureUsage('Delete');
              vm.remove();
            }
          });
      }

      function addReferenceLink() {
        $ExceptionlessClient.submitFeatureUsage(vm._source + '.addReferenceLink');
        return dialogs.create('app/stack/add-reference-dialog.tpl.html', 'AddReferenceDialog as vm').result.then(function (url) {
          function onSuccess() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.addReferenceLink.success').setProperty('url', url).submit();
            vm.stack.references.push(url);
          }

          function onFailure() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.addReferenceLink.error').setProperty('url', url).submit();
            notificationService.error(translateService.T('An error occurred while adding the reference link.'));
          }

          if (vm.stack.references.indexOf(url) < 0)
            return stackService.addLink(vm._stackId, url).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function buildUserStat(users, totalUsers) {
        if (totalUsers === 0) {
          return 0;
        }

        return $filter('percentage')((users / totalUsers * 100.0), 100);
      }

      function buildUserStatTitle(users, totalUsers) {
        return $filter('number')(users, 0) + ' of ' + $filter('number')(totalUsers, 0) +  ' users';
      }

      function executeAction() {
        var action = $stateParams.action;
        if (action === 'mark-fixed' && vm.stack.status !== 'fixed') {
          return updateFixed(true);
        }

        if ((action === 'ignored' || action === 'stop-notifications') && vm.stack.status !== 'ignored') {
          return updateIgnore();
        }

        if (action === 'discarded' && vm.stack.status !== 'discarded') {
          return updateDiscard();
        }
      }

      function canRefresh(data) {
        if (data && data.type === 'Stack' && data.id === vm._stackId) {
          return true;
        }

        if (data && data.type === 'PersistentEvent') {
          if (data.organization_id && data.organization_id !== vm.stack.organization_id) {
            return false;
          }
          if (data.project_id && data.project_id !== vm.stack.project_id) {
            return false;
          }

          if (data.stack_id && data.stack_id !== vm._stackId) {
            return false;
          }

          return true;
        }

        return false;
      }

      function get(data) {
        if (data && data.type === 'Stack' && data.deleted) {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Stack_Deleted', {stackId: vm._stackId}));
          return;
        }

        if (data && data.type === 'PersistentEvent') {
          return updateStats();
        }

        return getStack().then(updateStats).then(getProject);
      }

      function getOrganizations() {
        function onSuccess(response) {
          vm._organizations = response.data.plain();
          return vm._organizations;
        }

        return organizationService.getAll().then(onSuccess);
      }

      function getProject() {
        function onSuccess(response) {
          vm.project = response.data.plain();
          return vm.project;
        }

        return projectService.getById(vm.stack.project_id, true).then(onSuccess);
      }

      function getStack() {
        function onSuccess(response) {
          vm.stack = response.data.plain();
          vm.stack.references = vm.stack.references || [];
          addHotkeys();
        }

        function onFailure(response) {
          $state.go('app.frequent');

          if (response.status === 404) {
            notificationService.error(translateService.T('Cannot_Find_Stack', {stackId: vm._stackId}));
          } else {
            notificationService.error(translateService.T('Error_Load_Stack', {stackId: vm._stackId}));
          }
        }

        return stackService.getById(vm._stackId).then(onSuccess, onFailure);
      }

      function getProjectUserStats() {
        function optionsCallback(options) {
          options.filter = 'project:' + vm.stack.project_id;
          return options;
        }

        function onSuccess(response) {
          function getAggregationValue(data, name, defaultValue) {
            var aggs = data.aggregations;
            return aggs && aggs[name] && aggs[name].value || defaultValue;
          }

          vm._total_users = getAggregationValue(response.data, 'cardinality_user', 0);
          vm.stats.users = buildUserStat(vm._users, vm._total_users);
          vm.stats.usersTitle = buildUserStatTitle(vm._users, vm._total_users);
          return response;
        }

        return eventService.count('cardinality:user', false, optionsCallback).then(onSuccess);
      }

      function updateStats() {
        return getOrganizations().then(getStats);
      }

      function getStats() {
        function buildFields(options) {
          return ' cardinality:user ' + options.filter(function(option) { return option.selected; })
            .reduce(function(fields, option) { fields.push(option.field); return fields; }, [])
            .join(' ');
        }

        function optionsCallback(options) {
          options.filter = ['stack:' + vm._stackId, options.filter].filter(function(f) { return f && f.length > 0; }).join(' ');
          return options;
        }

        function onSuccess(response) {
          function getAggregationValue(data, name, defaultValue) {
            var aggs = data.aggregations;
            return aggs && aggs[name] && aggs[name].value || defaultValue;
          }

          function getAggregationItems(data, name, defaultValue) {
            var aggs = data.aggregations;
            return aggs && aggs[name] && aggs[name].items || defaultValue;
          }

          var results = response.data.plain();
          vm._users = getAggregationValue(results, 'cardinality_user', 0);
          vm.stats = {
            events: getAggregationValue(results, 'sum_count', 0),
            users: buildUserStat(vm._users, vm._total_users),
            usersTitle: buildUserStatTitle(vm._users, vm._total_users),
            first_occurrence: getAggregationValue(results, 'min_date'),
            last_occurrence: getAggregationValue(results, 'max_date')
          };

          var dateAggregation = getAggregationItems(results, 'date_date', []);
          var colors = ['rgba(124, 194, 49, .7)', 'rgba(60, 116, 0, .9)', 'rgba(89, 89, 89, .3)'];
          vm.chart.options.series = vm.chartOptions
            .filter(function(option) { return option.selected; })
            .reduce(function (series, option, index) {
              series.push({
                name: option.name,
                stroke: 'rgba(0, 0, 0, 0.15)',
                data: dateAggregation.map(function (item) {
                  function getYValue(item, index){
                    var field = option.field.replace(':', '_');
                    var proximity = field.indexOf('~');
                    if (proximity !== -1) {
                      field = field.substring(0, proximity);
                    }

                    return getAggregationValue(item, field, 0);
                  }

                  return { x: moment(item.key).unix(), y: getYValue(item, index), data: item };
                })
              });

              return series;
            }, [])
            .sort(function(a, b) {
              function calculateSum(previous, current) {
                return previous + current.y;
              }

              return b.data.reduce(calculateSum, 0) - a.data.reduce(calculateSum, 0);
            })
            .map(function(seri, index) {
              seri.color = colors[index];
              return seri;
            });

          return response;
        }

        var offset = filterService.getTimeOffset();
        return eventService.count('date:(date' + (offset ? '^' + offset : '') + buildFields(vm.chartOptions) + ') min:date max:date cardinality:user sum:count~1', false, optionsCallback).then(onSuccess).then(getProjectUserStats);
      }

      function hasSelectedChartOption() {
        return vm.chartOptions.filter(function (o) { return o.render && o.selected; }).length > 0;
      }

      function isValidDate(date) {
        var d = moment(date);
        return !!date && d.isValid() && d.year() > 1;
      }

      function promoteToExternal() {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteToExternal').setProperty('id', vm._stackId).submit();
        if (vm.project && !vm.project.has_premium_features) {
          var message = translateService.T('Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.');
          return billingService.confirmUpgradePlan(message, vm.stack.organization_id).then(function () {
            return promoteToExternal();
          }).catch(function(e){});
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteToExternal.success').setProperty('id', vm._stackId).submit();
          notificationService.success(translateService.T('Successfully promoted stack!'));
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.promoteToExternal.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message, vm.stack.organization_id).then(function () {
              return promoteToExternal();
            }).catch(function(e){});
          }

          if (response.status === 501) {
            return dialogService.confirm(response.data.message, translateService.T('Manage Integrations')).then(function () {
              $state.go('app.project.manage', { id: vm.stack.project_id });
            }).catch(function(e){});
          }

          notificationService.error(translateService.T('An error occurred while promoting this stack.'));
        }

        return stackService.promote(vm._stackId).then(onSuccess, onFailure);
      }

      function removeReferenceLink(reference) {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.removeReferenceLink').setProperty('id', vm._stackId).submit();
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete this reference link?'), translateService.T('DELETE REFERENCE LINK')).then(function () {
          function onSuccess() {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.removeReferenceLink.success').setProperty('id', vm._stackId).submit();
          }

          function onFailure(response) {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.removeReferenceLink.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
            notificationService.info(translateService.T('An error occurred while deleting the external reference link.'));
          }

          return stackService.removeLink(vm._stackId, reference).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function remove() {
        $ExceptionlessClient.createFeatureUsage(vm._source + '.remove').setProperty('id', vm._stackId).submit();
        var message = translateService.T('Are you sure you want to delete this stack (includes all stack events)?');
        return dialogService.confirmDanger(message, translateService.T('DELETE STACK')).then(function () {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully queued the stack for deletion.'));
            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.success').setProperty('id', vm._stackId).submit();
            $state.go('app.project-frequent', { projectId: vm.stack.project_id });
          }

          function onFailure(response) {
            $ExceptionlessClient.createFeatureUsage(vm._source + '.remove.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
            notificationService.error(translateService.T('An error occurred while deleting this stack.'));
          }

          return stackService.remove(vm._stackId).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function updateOpen() {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateOpen.success').setProperty('id', vm._stackId).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateOpen.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T('An error occurred while marking this stack as open.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateOpen').setProperty('id', vm._stackId).submit();
        if (vm.stack.status === 'open') {
          return;
        }

        return stackService.changeStatus(vm._stackId, 'open').then(onSuccess, onFailure);
      }

      function updateCritical() {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateCritical.success').setProperty('id', vm._stackId).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateCritical.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T(vm.stack.occurrences_are_critical ? 'An error occurred while marking future occurrences as not critical.' : 'An error occurred while marking future occurrences as critical.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateCritical').setProperty('id', vm._stackId).submit();
        if (vm.stack.occurrences_are_critical) {
          return stackService.markNotCritical(vm._stackId).then(onSuccess, onFailure);
        }

        return stackService.markCritical(vm._stackId).catch(onSuccess, onFailure);
      }

      function updateDiscard() {
        if (vm.stack.status === 'discarded') {
          return updateOpen();
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateDiscard.success').setProperty('id', vm._stackId).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateDiscard.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T('An error occurred while marking this stack as discarded.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateDiscard').setProperty('id', vm._stackId).submit();
        var message = translateService.T('Are you sure you want to all current stack events and discard any future stack events?') + ' ' + translateService.T('All future occurrences will be discarded and will not count against your event limit.');
        return dialogService.confirmDanger(message, translateService.T('Discard')).then(function () {
          return stackService.changeStatus(vm._stackId, 'discarded').then(onSuccess, onFailure);
          }).catch(function(e){});
      }

      function updateFixed(showSuccessNotification) {
        if (vm.stack.status === 'fixed') {
          return updateOpen();
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateFixed.success').setProperty('id', vm._stackId).submit();
          if (!showSuccessNotification) {
            return;
          }

          notificationService.info(translateService.T('Successfully marked the stack as fixed.'));
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateFixed.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T('An error occurred while marking this stack as fixed.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateFixed').setProperty('id', vm._stackId).submit();
        return stackDialogService.markFixed().then(function (version) {
          return stackService.markFixed(vm._stackId, version).then(onSuccess, onFailure).catch(function(e){});
        }).catch(function(e){});
      }

      function updateSnooze(timePeriod) {
        if (!timePeriod && vm.stack.status === 'snoozed') {
          return updateOpen();
        }

        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateSnooze.success').setProperty('id', vm._stackId).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateSnooze.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T(vm.stack.status === 'snoozed' ? 'An error occurred while marking this stack as open.' : 'An error occurred while marking this stack as snoozed.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateSnooze').setProperty('id', vm._stackId).submit();
        var snoozeUntilUtc = moment();
        switch (timePeriod) {
          case "6hours":
            snoozeUntilUtc = snoozeUntilUtc.add(6, 'hours');
            break;
          case "day":
            snoozeUntilUtc = snoozeUntilUtc.add(1, 'days');
            break;
          case "week":
            snoozeUntilUtc = snoozeUntilUtc.add(1, 'weeks');
            break;
          case "month":
          default:
            snoozeUntilUtc = snoozeUntilUtc.add(1, 'months');
            break;
        }

        return stackService.markSnoozed(vm._stackId, snoozeUntilUtc.format('YYYY-MM-DDTHH:mm:ssz')).then(onSuccess, onFailure);
      }

      function updateIgnore() {
        function onSuccess() {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateIgnore.success').setProperty('id', vm._stackId).submit();
        }

        function onFailure(response) {
          $ExceptionlessClient.createFeatureUsage(vm._source + '.updateIgnore.error').setProperty('id', vm._stackId).setProperty('response', response).submit();
          notificationService.error(translateService.T(vm.stack.status === 'snoozed' ? 'An error occurred while marking this stack as open.' : 'An error occurred while marking this stack as ignored.'));
        }

        $ExceptionlessClient.createFeatureUsage(vm._source + '.updateIgnore').setProperty('id', vm._stackId).submit();
        var ignored = vm.stack.status === 'ignored';
        return stackService.changeStatus(vm._stackId, ignored ? 'open' : 'ignored').then(onSuccess, onFailure);
      }

      function showActionIcons() {
        return vm.stack.occurrences_are_critical;
      }

      function showAllTimeEvents() {
        return vm.stats.events !== vm.stack.total_occurrences;
      }

      function showAllTimeFirstOccurrence() {
        return !moment(vm.stats.first_occurrence).isSame(moment(vm.stack.first_occurrence));
      }

      function showAllTimeLastOccurrence() {
        return showAllTimeFirstOccurrence() || !moment(vm.stats.last_occurrence).isSame(moment(vm.stack.last_occurrence));
      }

      function showAllTimeRow() {
        return vm.stack.first_occurrence && showAllTimeEvents() || showAllTimeFirstOccurrence() || showAllTimeLastOccurrence();
      }

      this.$onInit = function $onInit() {
        vm._organizations = [];
        vm._source = 'app.stack.Stack';
        vm._stackId = $stateParams.id;
        vm.addReferenceLink = addReferenceLink;

        vm.chart = {
          options: {
            padding: {top: 0.085},
            renderer: 'stack',
            stroke: true,
            unstack: true
          },
          features: {
            hover: {
              render: function (args) {
                var date = moment.unix(args.domainX);
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
                  .setProperty('id', vm._stackId)
                  .setProperty('start', start)
                  .setProperty('end', end)
                  .submit();

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

        vm.chartOptions = [
          {name: translateService.T('Occurrences'), field: 'sum:count~1', title: '', selected: true, render: false},
          {name: translateService.T('Average Value'), field: 'avg:value', title: translateService.T('The average of all event values'), render: true, menuName: translateService.T('Show Average Value')},
          {name: translateService.T('Value Sum'), field: 'sum:value', title: translateService.T('The sum of all event values'), render: true, menuName: translateService.T('Show Value Sum')}
        ];

        vm.canRefresh = canRefresh;
        vm.get = get;
        vm.updateStats = updateStats;
        vm.hasSelectedChartOption = hasSelectedChartOption;
        vm.isValidDate = isValidDate;
        vm.promoteToExternal = promoteToExternal;
        vm.project = {};
        vm.remove = remove;
        vm.removeReferenceLink = removeReferenceLink;
        vm.recentOccurrences = {
          canRefresh: function canRefresh(events, data) {
            if (data.type === 'PersistentEvent') {
              // We are already listening to the stack changed event... This prevents a double refresh.
              if (!data.deleted) {
                return false;
              }

              // Refresh if the event id is set (non bulk) and the deleted event matches one of the events.
              if (!!data.id && !!events) {
                return events.filter(function (e) { return e.id === data.id; }).length > 0;
              }

              if (data.stack_id === vm._stackId) {
                return true;
              }

              return data.project_id ? vm.stack.project_id === data.project_id : vm.stack.organization_id === data.organization_id;
            }

            if (data.type === 'Stack') {
              return data.id === vm._stackId;
            }

            if (data.type === 'Project') {
              return vm.stack.project_id === data.id;
            }

            return data.type === 'Organization' && vm.stack.organization_id === data.id;
          },
          get: function (options) {
            return eventService.getByStackId(vm._stackId, options);
          },
          summary: {
            showStatus: false,
            showType: false
          },
          options: {
            limit: 10,
            mode: 'summary'
          },
          source: vm._source + '.Events'
        };
        vm.showActionIcons = showActionIcons;
        vm.showAllTimeEvents = showAllTimeEvents;
        vm.showAllTimeFirstOccurrence = showAllTimeFirstOccurrence;
        vm.showAllTimeLastOccurrence = showAllTimeLastOccurrence;
        vm.showAllTimeRow = showAllTimeRow;

        vm.stack = {};
        vm.stats = {
          events: 0,
          users: buildUserStat(0, 0),
          usersTitle: buildUserStatTitle(0, 0),
          first_occurrence: undefined,
          last_occurrence: undefined
        };

        vm._users = 0;
        vm._total_users = 0;
        vm.updateOpen = updateOpen;
        vm.updateCritical = updateCritical;
        vm.updateDiscard = updateDiscard;
        vm.updateFixed = updateFixed;
        vm.updateSnooze = updateSnooze;
        vm.updateIgnore = updateIgnore;

        get().then(executeAction);
      };
    });
}());
