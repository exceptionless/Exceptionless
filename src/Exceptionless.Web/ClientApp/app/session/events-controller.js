/* global Rickshaw:false */
(function () {
  'use strict';

  angular.module('app.session')
    .controller('session.Events', function ($ExceptionlessClient, eventService, $filter, filterService, translateService) {
      var vm = this;
      function get() {
        function optionsCallback(options) {
          if (vm.includeLiveFilter) {
            options.filter += ' _missing_:data.sessionend';
          }

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
          vm.stats = {
            total: $filter('number')(results.total, 0),
            users: $filter('number')(getAggregationValue(results, 'cardinality_user', 0), 0),
            avg_duration: getAggregationValue(results, 'avg_value'),
            avg_per_hour: $filter('number')(eventService.calculateAveragePerHour(results.total, vm._organizations), 1)
          };

          var dateAggregation = getAggregationItems(results, 'date_date', []);
          vm.chart.options.series[0].data = dateAggregation.map(function (item) {
            return {x: moment(item.key).unix(), y: getAggregationValue(item, 'cardinality_user', 0), data: item};
          });

          vm.chart.options.series[1].data = dateAggregation.map(function (item) {
            return {x: moment(item.key).unix(), y: item.total || 0, data: item};
          });
        }

        var offset = filterService.getTimeOffset();
        return eventService.count('avg:value cardinality:user date:(date' + (offset ? '^' + offset : '') + ' cardinality:user)', false, optionsCallback).then(onSuccess).catch(function(e){});
      }

      function updateLiveFilter() {
        vm.includeLiveFilter = !vm.includeLiveFilter;
        filterService.fireFilterChanged(false);
      }

      this.$onInit = function $onInit() {
        vm._source = 'app.session.Events';
        vm.chart = {
          options: {
            padding: {top: 0.085},
            renderer: 'stack',
            series: [{
              name: translateService.T('Users'),
              color: 'rgba(60, 116, 0, .9)',
              stroke: 'rgba(0, 0, 0, 0.15)'
            }, {
              name: translateService.T('Sessions'),
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
        vm.includeLiveFilter = false;
        vm.updateLiveFilter = updateLiveFilter;
        vm.recentSessions = {
          get: function (options) {
            function optionsCallback(options) {
              if (vm.includeLiveFilter) {
                options.filter += ' _missing_:data.sessionend';
              }

              return options;
            }

            return eventService.getAllSessions(options, optionsCallback);
          },
          summary: {
            showStatus: false,
            showType: false
          },
          options: {
            limit: 10,
            mode: 'summary'
          },
          source: vm._source + '.Events',
          hideActions: true
        };
        vm.stats = {
          total: 0,
          users: 0,
          avg_duration: undefined,
          avg_per_hour: 0.0
        };
        get();
      };
    });
}());
