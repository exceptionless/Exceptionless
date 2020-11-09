/* global Rickshaw:false */
(function () {
  'use strict';

  angular.module('app.organization')
    .controller('organization.Manage', function ($ExceptionlessClient, filterService, $filter, $state, $stateParams, $window, billingService, dialogService, organizationService, projectService, userService, notificationService, translateService, dialogs, STRIPE_PUBLISHABLE_KEY) {
      var vm = this;
      function activateTab(tabName) {
        switch (tabName) {
          case 'projects':
            vm.activeTabIndex = 1;
            break;
          case 'users':
            vm.activeTabIndex = 2;
            break;
          case 'billing':
            vm.activeTabIndex = 3;
            break;
          default:
            vm.activeTabIndex = 0;
            break;
        }
      }

      function addUser() {
        return dialogs.create('app/organization/manage/add-user-dialog.tpl.html', 'AddUserDialog as vm').result.then(createUser);
      }

      function changePlan() {
        return billingService.changePlan(vm.organization.id).catch(function(e){});
      }

      function createUser(emailAddress) {
        function onFailure(response) {
          if (response.status === 426) {
            return billingService.confirmUpgradePlan(response.data.message, vm._organizationId).then(function() {
              return createUser(emailAddress);
            }).catch(function(e){});
          }

          var message = translateService.T('An error occurred while inviting the user.');
          if (response.data && response.data.message) {
            message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
          }

          notificationService.error(message);
        }

        return organizationService.addUser(vm._organizationId, emailAddress).catch(onFailure);
      }

      function get(data) {
        if (vm._ignoreRefresh) {
          return;
        }

        if (data && data.type === 'Organization' && data.deleted && data.id === vm._organizationId) {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Organization_Deleted', {organizationId : vm._organizationId}));
          return;
        }

        return getOrganization();
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
          vm.organization.usage = vm.organization.usage || [{ date: moment.utc().startOf('month').toISOString(), total: 0, blocked: 0, limit: vm.organization.max_events_per_month, too_big: 0 }];
          vm.hasMonthlyUsage = vm.organization.max_events_per_month > 0;
          vm.remainingEventLimit = getRemainingEventLimit(vm.organization);
          vm.canChangePlan = !!STRIPE_PUBLISHABLE_KEY && vm.organization;

          vm.chart.options.series[0].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.total - item.blocked - item.too_big, data: item};
          });

          vm.chart.options.series[1].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.blocked, data: item};
          });

          vm.chart.options.series[2].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.too_big, data: item};
          });

          vm.chart.options.series[3].data = vm.organization.usage.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.limit, data: item};
          });

          return vm.organization;
        }

        function onFailure() {
          $state.go('app.frequent');
          notificationService.error(translateService.T('Cannot_Find_Organization', {organizationId : vm._organizationId}));
        }

        return organizationService.getById(vm._organizationId, false).then(onSuccess, onFailure);
      }


      function hasAdminRole(user) {
        return userService.hasAdminRole(user);
      }

      function leaveOrganization(currentUser){
        return dialogService.confirmDanger(translateService.T('Are you sure you want to leave this organization?'), translateService.T('Leave Organization')).then(function () {
          function onSuccess() {
            $state.go('app.organization.list');
          }

          function onFailure(response) {
            var message = translateService.T('An error occurred while trying to leave the organization.');
            if (response.status === 400) {
              message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
            }

            notificationService.error(message);
            vm._ignoreRefresh = false;
          }

          vm._ignoreRefresh = true;
          return organizationService.removeUser(vm._organizationId, currentUser.email_address).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function removeOrganization() {
        return dialogService.confirmDanger(translateService.T('Are you sure you want to delete the "' + vm.organization.name + '" organization?'), translateService.T('Delete Organization')).then(function () {
          function onSuccess() {
            notificationService.info(translateService.T('Successfully queued the organization for deletion.'));
            $state.go('app.organization.list');
          }

          function onFailure(response) {
            var message = translateService.T('An error occurred while trying to delete the organization.');
            if (response.status === 400) {
              message += ' ' + translateService.T('Message:') + ' ' + response.data.message;
            }

            notificationService.error(message);
            vm._ignoreRefresh = false;
          }

          vm._ignoreRefresh = true;
          return organizationService.remove(vm._organizationId).then(onSuccess, onFailure);
        }).catch(function(e){});
      }

      function save(isValid) {
        if (!isValid) {
          return;
        }

        function onFailure() {
          notificationService.error(translateService.T('An error occurred while saving the organization.'));
        }

        return organizationService.update(vm._organizationId, vm.organization).catch(onFailure);
      }

      this.$onInit = function $onInit() {
        vm._source = 'organization.Manage';
        vm._ignoreRefresh = false;
        vm._organizationId = $stateParams.id;

        vm.activeTabIndex = 0;
        vm.addUser = addUser;
        vm.canChangePlan = false;
        vm.changePlan = changePlan;
        vm.chart = {
          options: {
            padding: {top: 0.085},
            renderer: 'multi',
            series: [{
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

                content += '<span class="detail-swatch"></span>' + $filter('number')(args.detail[0].value.data.total) + ' Total<br />';

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

                $state.go('app.organization-frequent', { organizationId: vm.organization.id });
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
        vm.get = get;
        vm.hasAdminRole = hasAdminRole;
        vm.hasMonthlyUsage = true;
        vm.invoices = {
          get: function (options, useCache) {
            return organizationService.getInvoices(vm._organizationId, options, useCache);
          },
          options: {
            limit: 12
          },
          organizationId: vm._organizationId
        };
        vm.leaveOrganization = leaveOrganization;
        // NOTE: this is currently the end of each month until we change our system to use the plan changed date.
        vm.next_billing_date = moment().startOf('month').add(1, 'months').toDate();
        vm.organization = {};
        vm.organizationForm = {};
        vm.projects = {
          get: function (options, useCache) {
            return projectService.getByOrganizationId(vm._organizationId, options, useCache);
          },
          organization: vm._organizationId,
          options: {
            limit: 10,
            mode: 'stats'
          }
        };
        vm.remainingEventLimit = 3000;
        vm.removeOrganization = removeOrganization;
        vm.save = save;
        vm.users = {
          get: function (options, useCache) {
            return userService.getByOrganizationId(vm._organizationId, options, useCache);
          },
          options: {
            limit: 10
          },
          organizationId: vm._organizationId
        };

        activateTab($stateParams.tab);
        get();
      };
    });
}());
