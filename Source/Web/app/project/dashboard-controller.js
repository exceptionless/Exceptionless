(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Dashboard', ['$filter', '$stateParams', 'eventService', 'notificationService', 'stackService', 'statService', function ($filter, $stateParams, eventService, notificationService, stackService, statService) {
            var projectId = $stateParams.id;

            function getStats() {
                function onSuccess(response) {
                    vm.stats = response.data.plain();
                    vm.stats.timeline.push({ "date": "2014-10-24T00:00:00", "total": 9, "unique": 7, "new": 7 });
                    vm.stats.timeline.push({ "date": "2014-10-24T10:00:00", "total": 2, "unique": 2, "new": 1 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:00:00", "total": 2, "unique": 4, "new": 0 });


                    vm.series[0].data = vm.stats.timeline.map(function (item) {
                        return { x: moment.utc(item.date).unix(), y: item.total, data: item };
                    });

                    vm.series[1].data = vm.stats.timeline.map(function (item) {
                        return { x: moment.utc(item.date).unix(), y: item.unique, data: item };
                    });
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
                    onRender: function (args) {
                        var date = moment.unix(args.domainX).utc();
                        var formattedDate = date.hours() === 0 ? $filter('date')(date.toDate(), 'medium') : $filter('date')(date.toDate(), 'medium');
                        var content = '<div class="date">' + formattedDate + '</div>';
                        args.detail.sort(function (a, b) {
                            return a.order - b.order;
                        }).forEach(function (d) {
                            var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                            content += swatch + $filter('number')(d.name === 'Total' ? d.value.data.total : d.value.data.unique) + ' ' + d.series.name + ' <br />';
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
                        console.log(position);
                        //var start = DateUtil.roundToPrevious15Minutes(moment.unix(position.coordMinX).utc());
                      //  var end = DateUtil.roundToNext15Minutes(moment.unix(position.coordMaxX).utc());
                       // this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

                        return false;
                    }
                },
                yAxis: {
                    ticks: 5,
                    tickFormat: 'formatKMBT',
                    ticksTreatment: 'glow'
                }
            };

            vm.series = [
                {
                    name: 'Total',
                    color: 'rgba(115, 192, 58, 0.5)',
                    stroke: 'rgba(0,0,0,0.15)'
                }, {
                    name: 'Unique',
                    color: 'rgba(95, 157, 47, 0.5)',
                    stroke: 'rgba(0,0,0,0.15)'
                }
            ];

            getStats();
        }
    ]);
}());
