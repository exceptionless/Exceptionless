(function () {
  'use strict';

  angular.module('app.stack')
    .controller('Stack', ['$filter', '$state', '$stateParams', 'dialogs', 'dialogService', 'eventService', 'featureService', 'filterService', 'notificationService', 'stackService', 'statService', function ($filter, $state, $stateParams, dialogs, dialogService, eventService, featureService, filterService, notificationService, stackService, statService) {
      var stackId = $stateParams.id;
      var vm = this;

      function addReferenceLink() {
        dialogs.create('app/stack/add-reference-dialog.tpl.html', 'AddReferenceDialog as vm').result.then(function (url) {
          function onSuccess() {
            vm.stack.references.push(url);
          }

          function onFailure() {
            notificationService.error('An error occurred while adding the reference link.');
          }

          if (vm.stack.references.indexOf(url) < 0)
            return stackService.addLink(stackId, url).then(onSuccess, onFailure);
        });
      }

      function get() {
        return getStack().then(getStats);
      }

      function getStack() {
        function onSuccess(response) {
          vm.stack = response.data.plain();
        }

        function onFailure() {
          $state.go('app.dashboard');
          notificationService.error('The stack "' + $stateParams.id + '" could not be found.');
        }

        return stackService.getById(stackId).then(onSuccess, onFailure);
      }

      function getStats() {
        function onSuccess(response) {
          vm.stats = response.data.plain();

          vm.chart.options.series[0].data = vm.stats.timeline.map(function (item) {
            return {x: moment.utc(item.date).unix(), y: item.total, data: item};
          });
        }

        function onFailure() {
          notificationService.error('An error occurred while loading the stats for this stack.');
        }

        var options = {};
        return statService.getByStackId(stackId, options).then(onSuccess, onFailure);
      }

      function hasTags() {
        return vm.stack.tags && vm.stack.tags.length > 0;
      }

      function hasReference() {
        return vm.stack.references && vm.stack.references.length > 0;
      }

      function hasReferences() {
        return vm.stack.references && vm.stack.references.length > 1;
      }

      function isCritical() {
        return vm.stack.occurrences_are_critical === true;
      }

      function isFixed() {
        return vm.stack.date_fixed;
      }

      function isHidden() {
        return vm.stack.is_hidden === true;
      }

      function isRegressed() {
        return vm.stack.is_regressed === true;
      }

      function notificationsDisabled() {
        return vm.stack.disable_notifications === true;
      }

      function promoteToExternal() {
        if (!featureService.hasPremium()) {
          return dialogService.confirmUpgradePlan('Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.').then(function () {
            return promoteToExternal();
          });
        }

        function onSuccess() {
          notificationService.success('Successfully promoted stack!');
        }

        function onFailure(response) {
          if (response.status === 426) { // TODO: Move this to an interceptor.
            return dialogService.confirmUpgradePlan(response.data.message).then(function () {
              return promoteToExternal();
            });
          }

          if (response.status === 501) {
            return dialogService.confirm(response.data.message, 'Manage Integrations').then(function () {
              $state.go('app.project.manage', { id: vm.stack.project_id });
            });
          }

          notificationService.error('An error occurred while promoting this stack.');
        }

        return stackService.promote(stackId).then(onSuccess, onFailure);
      }

      function removeReferenceLink(reference) {
        return dialogService.confirmDanger('Are you sure you want to remove this reference link?', 'REMOVE REFERENCE LINK').then(function () {
          function onSuccess() {
            vm.stack.references.splice(vm.stack.references.indexOf(reference), 1);
          }

          function onFailure() {
            notificationService.info('An error occurred while removing the external reference link.');
          }

          return stackService.removeLink(stackId, reference).then(onSuccess, onFailure);
        });
      }

      function remove() {
        var message = 'Are you sure you want to delete this stack?';
        return dialogService.confirmDanger(message, 'DELETE STACK').then(function () {
          function onSuccess() {
            $state.go('app.project-dashboard', { projectId: vm.stack.project_id });
          }

          function onFailure() {
            notificationService.error('An error occurred while deleting this stack.');
          }

          return stackService.remove(stackId).then(onSuccess, onFailure);
        });
      }

      function updateIsCritical() {
        function onSuccess() {
          vm.stack.occurrences_are_critical = !isCritical();
        }

        function onFailure() {
          notificationService.error('An error occurred while marking future occurrences as ' + isCritical() ? 'not critical.' : 'critical.');
        }

        if (isCritical()) {
          return stackService.markNotCritical(stackId).then(onSuccess, onFailure);
        }

        return stackService.markCritical(stackId).then(onSuccess, onFailure);
      }

      function updateIsFixed() {
        function onSuccess() {
          vm.stack.date_fixed = !isFixed() ? moment().toDate() : null;
          if (isRegressed() && isFixed())
            vm.stack.is_regressed = false;
        }

        function onFailure() {
          var action = isFixed() ? ' not' : '';
          notificationService.error('An error occurred while marking this stack as' + action + ' fixed.');
        }

        if (isFixed()) {
          return stackService.markNotFixed(stackId).then(onSuccess, onFailure);
        }

        return stackService.markFixed(stackId).then(onSuccess, onFailure);
      }

      function updateIsHidden() {
        function onSuccess() {
          vm.stack.is_hidden = !isHidden();
        }

        function onFailure() {
          notificationService.error('An error occurred while marking this stack as ' + isHidden() ? 'shown.' : 'hidden.');
        }

        if (isHidden()) {
          return stackService.markNotHidden(stackId).then(onSuccess, onFailure);
        }

        return stackService.markHidden(stackId).then(onSuccess, onFailure);
      }

      function updateNotifications() {
        function onSuccess() {
          vm.stack.disable_notifications = !notificationsDisabled();
        }

        function onFailure() {
          var action = notificationsDisabled() ? 'enabling' : 'disabling';
          notificationService.error('An error occurred while ' + action + ' stack notifications.');
        }

        if (notificationsDisabled()) {
          return stackService.enableNotifications(stackId).then(onSuccess, onFailure);
        }

        return stackService.disableNotifications(stackId).then(onSuccess, onFailure);
      }

      vm.addReferenceLink = addReferenceLink;

      vm.chart = {
        options: {
          renderer: 'stack',
          stroke: true,
          padding: {top: 0.085},
          series: [{
            name: 'Occurrences',
            color: 'rgba(124, 194, 49, .9)',
            stroke: 'rgba(0, 0, 0, 0.15)'
          }]
        },
        features: {
          hover: {
            render: function (args) {
              var date = moment.unix(args.domainX).utc();
              var formattedDate = date.hours() === 0 ? $filter('date')(date.toDate(), 'mediumDate') : $filter('date')(date.toDate(), 'medium');
              var content = '<div class="date">' + formattedDate + '</div>';
              args.detail.sort(function (a, b) {
                return a.order - b.order;
              }).forEach(function (d) {
                var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                content += swatch + $filter('number')(d.value.data.total) + ' ' + d.series.name + ' <br />';
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
              var start = moment.unix(position.coordMinX).utc();
              var end = moment.unix(position.coordMaxX).utc();

              filterService.setTime(start.format('YYYY-MM-DDTHH:mm:ss') + '-' + end.format('YYYY-MM-DDTHH:mm:ss'));

              return false;
            }
          },
          yAxis: {
            ticks: 5,
            tickFormat: 'formatKMBT',
            ticksTreatment: 'glow'
          }
        }
      };

      vm.get = get;
      vm.hasTags = hasTags;
      vm.hasReference = hasReference;
      vm.hasReferences = hasReferences;
      vm.isCritical = isCritical;
      vm.isFixed = isFixed;
      vm.isHidden = isHidden;
      vm.isRegressed = isRegressed;
      vm.notificationsDisabled = notificationsDisabled;
      vm.promoteToExternal = promoteToExternal;
      vm.remove = remove;
      vm.removeReferenceLink = removeReferenceLink;
      vm.recentOccurrences = {
        get: function (options) {
          return eventService.getByStackId(stackId, options);
        },
        options: {
          limit: 10,
          mode: 'summary'
        }
      };
      vm.stack = {};
      vm.stats = {};
      vm.updateIsCritical = updateIsCritical;
      vm.updateIsFixed = updateIsFixed;
      vm.updateIsHidden = updateIsHidden;
      vm.updateNotifications = updateNotifications;

      get();
    }]);
}());
