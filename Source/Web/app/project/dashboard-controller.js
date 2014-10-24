(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Dashboard', ['$filter', '$stateParams', 'eventService', 'notificationService', 'stackService', 'statService', function ($filter, $stateParams, eventService, notificationService, stackService, statService) {
            var projectId = $stateParams.id;

            function getStats() {
                function onSuccess(response) {
                    vm.stats = response.data.plain();
                }

                function onFailure() {
                    notificationService.error('An error occurred while loading the stats for your project.');
                }

                var options = {};
                return statService.getByProjectId(projectId, options).then(onSuccess, onFailure);
            }

            var vm = this;
            vm.mostFrequent = {
                get: function (options) {
                    return stackService.getFrequentByProjectId(projectId, options);
                },
                options: {
                    limit: 5,
                    mode: 'summary'
                }
            };

            vm.mostRecent = {
                header: 'Most Recent',
                get: function (options) {
                    return eventService.getAll(options);
                },
                options: {
                    limit: 5,
                    mode: 'summary'
                }
            };
            vm.stats = {};


            /*

             var rangeSelector = this.createChartRangeSelector(this._graph);

             return new Rickshaw.Graph.RangeSelector({
             graph: graph,
             selectionCallback: (position: any) => {
             var start = DateUtil.roundToPrevious15Minutes(moment.unix(position.coordMinX).utc());
             var end = DateUtil.roundToNext15Minutes(moment.unix(position.coordMaxX).utc());
             this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

             return false;
             }
             });


            public updateChart() {
                this.chartOptions.series[0].data = [];
                this.chartOptions.series[1].data = [];

                var stats = this._stats();
                for (var x = 0; x < stats.length; x++) {
                    this.chartOptions.series[0].data.push({ x: moment.utc(stats[x].Date).unix(), y: stats[x].Total, data: stats[x] });
                    this.chartOptions.series[1].data.push({ x: moment.utc(stats[x].Date).unix(), y: stats[x].UniqueTotal, data: stats[x] });
                }

                this.chart.update();
            }

             this._graph = new Rickshaw.Graph(this.chartOptions);
             this._graph.renderer.unstack = true;
             this._graph.render();

             var hoverDetail = this.createChartHoverDetail(this._graph);
             var legend = this.createChartLegend(this._graph);
             var highlighter = this.createChartHighlight(this._graph, legend);
             this.chartTimeline = this.createChartAnnotate(this._graph);
             var rangeSelector = this.createChartRangeSelector(this._graph);

             this.renderChartAxis(this._graph);

            */

            vm.options = {
                renderer: 'stack',
                stroke: true,
                padding: { top: 0.085 }
            };

            vm.features = {
                hover: {
                    /*formatter: function (series, x, y, formattedX, formattedY, d) {
                        var date = moment.unix(x).utc();
                        var formattedDate = date.hours() === 0 ? $filter('date')(date.date(), 'YYYY-MM-DD') : $filter('date')(date.date(), 'medium');
                        var swatch = '<span class="detail-swatch" style="background-color: ' + series.color + '"></span>';
                        var content = swatch + series.name + ": " + $filter('number')(y, '0,0[.]0') + ' <br /><span class="date">' + formattedDate + '</span>';
                        return content;
                    },*/
                    render: function (args) {
                        var date = moment.unix(args.domainX).utc();
                        var formattedDate = date.hours() === 0 ? $filter('date')(date.date(), 'YYYY-MM-DD') : $filter('date')(date.date(), 'medium');
                        var content = '<div class="date">' + formattedDate + '</div>';
                        args.detail.sort(function (a, b) {
                            return a.order - b.order;
                        }).forEach(function (d) {
                            var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                            content += swatch + $filter('number')(d.name === 'Exceptions' ? d.value.data.Total : d.value.data.UniqueTotal).format('0,0[.]0') + ' ' + d.series.name + ' <br />';
                        }, this);

                        var xLabel = document.createElement('div');
                        xLabel.className = 'x_label';
                        xLabel.innerHTML = content;
                        this.element.appendChild(xLabel);

                        this.show();
                    }
                },
                range: {
                    onSelection: function (position) {
                        {
                            //var start = DateUtil.roundToPrevious15Minutes(moment.unix(position.coordMinX).utc());
                          //  var end = DateUtil.roundToNext15Minutes(moment.unix(position.coordMaxX).utc());
                           // this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

                            return false;
                        }
                    }
                },
                yAxis: {
                    ticks: 5,
                    tickFormat: 'formatKMBT',
                    ticksTreatment: 'glow'
                }
            };

            vm.series = [{
                name: 'Events',
                color: 'rgba(115, 192, 58, 0.5)',
                stroke: 'rgba(0,0,0,0.15)',
                data: [{x: 0, y: 23}, {x: 1, y: 15}, {x: 2, y: 79}, {x: 3, y: 31}, {x: 4, y: 60}]
            }, {
                name: 'Unique',
                color: 'rgba(95, 157, 47, 0.5)',
                stroke: 'rgba(0,0,0,0.15)',
                data: [{x: 0, y: 30}, {x: 1, y: 20}, {x: 2, y: 64}, {x: 3, y: 50}, {x: 4, y: 15}]
            }];

            getStats();
        }
    ]);
}());
