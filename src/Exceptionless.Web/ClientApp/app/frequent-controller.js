/* global Rickshaw:false */
(function () {
  'use strict';

  angular.module('app')
    .controller('app.Frequent', function ($ExceptionlessClient, $filter, $stateParams, eventService, filterService, notificationService, organizationService, stackService, translateService) {
      var vm = this;
      function canRefresh(data) {
        if (!!data && data.type === 'PersistentEvent' || data.type === 'Stack') {
          return filterService.includedInProjectOrOrganizationFilter({ organizationId: data.organization_id, projectId: data.project_id });
        }

        if (!!data && data.type === 'Organization' || data.type === 'Project') {
          return filterService.includedInProjectOrOrganizationFilter({organizationId: data.id, projectId: data.id});
        }

        return !data;
      }

      function get() {
        return getOrganizations().then(getStats).catch(function(e){});
      }

      function getStats() {
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
          var termsAggregation = getAggregationItems(results, 'terms_first', []);
          var count = getAggregationValue(results, 'sum_count', 0);
          vm.stats = {
            events: $filter('number')(count, 0),
            stacks: $filter('number')(getAggregationValue(results, 'cardinality_stack', 0), 0),
            newStacks: $filter('number')(termsAggregation.length > 0 ? termsAggregation[0].total : 0, 0),
            avg_per_hour: $filter('number')(eventService.calculateAveragePerHour(count, vm._organizations), 1)
          };

          var dateAggregation = getAggregationItems(results, 'date_date', []);
          vm.chart.options.series[0].data = dateAggregation.map(function (item) {
            return {x: moment(item.key).unix(), y: getAggregationValue(item, 'cardinality_stack', 0), data: item};
          });

          vm.chart.options.series[1].data = dateAggregation.map(function (item) {
            return {x: moment(item.key).unix(), y: getAggregationValue(item, 'sum_count', 0), data: item};
          });
        }

        var offset = filterService.getTimeOffset();
        return eventService.count('date:(date' + (offset ? '^' + offset : '') + ' cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1', true).then(onSuccess);
      }

      function getOrganizations() {
        function onSuccess(response) {
          vm._organizations = response.data.plain();
          return vm._organizations;
        }

        return organizationService.getAll().then(onSuccess);
      }

      this.$onInit = function $onInit() {
        vm._organizations = [];
        vm._source = 'app.Frequent';
        vm.canRefresh = canRefresh;
        vm.chart = {
          options: {
            padding: {top: 0.085},
            renderer: 'stack',
            series: [{
              name: translateService.T('Stacks'),
              color: 'rgba(60, 116, 0, .9)',
              stroke: 'rgba(0, 0, 0, 0.15)'
            }, {
              name: translateService.T('Events'),
              color: 'rgba(124, 194, 49, .7)',
              stroke: 'rgba(0, 0, 0, 0.15)'
            }
            ],
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
                  content += swatch + $filter('number')(d.formattedYValue) + ' ' + d.series.name + ' <br />';
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
        vm.get = get;

        vm.mostFrequent = {
          header: 'Most Frequent',
          get: eventService.getAll,
          options: {
            limit: 15,
            mode: 'stack_frequent'
          },
          source: vm._source + '.Events'
        };
        vm.stats = {
          events: 0,
          stacks: 0,
          newStacks: 0,
          avg_per_hour: 0.0
        };
        vm.type = $stateParams.type || 'all';

        get();
      };
    });
}());
