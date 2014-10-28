(function () {
    'use strict';

    angular.module('app.project')
        .controller('project.Dashboard', ['$filter', '$stateParams', 'eventService', 'notificationService', 'stackService', 'statService', function ($filter, $stateParams, eventService, notificationService, stackService, statService) {
            var projectId = $stateParams.id;
            var vm = this;

            function getStats() {
                function onSuccess(response) {
                    vm.stats = response.data.plain();
                    vm.stats.timeline.push({ "date": "2014-10-24T00:00:00", "total": 9, "unique": 7, "new": 7 });
                    vm.stats.timeline.push({ "date": "2014-10-24T10:00:00", "total": 2, "unique": 2, "new": 1 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:06:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:07:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:08:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:09:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:10:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:20:00", "total": 3, "unique": 5, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:30:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:40:00", "total": 2, "unique": 4, "new": 0 });
                    vm.stats.timeline.push({ "date": "2014-10-25T02:50:00", "total": 4, "unique": 5, "new": 0 });

                    vm.chart.options.series[0].data = vm.stats.timeline.map(function (item) {
                        return { x: moment.utc(item.date).unix(), y: item.total, data: item };
                    });

                    vm.chart.options.series[1].data = vm.stats.timeline.map(function (item) {
                        return { x: moment.utc(item.date).unix(), y: item.unique, data: item };
                    });
                }

                function onFailure() {
                    notificationService.error('An error occurred while loading the stats for your project.');
                }

                var options = {};
                return statService.getByProjectId(projectId, options).then(onSuccess, onFailure);
            }

            vm.chart = {
                options: {
                    renderer: 'stack',
                    stroke: true,
                    padding: { top: 0.085 },
                    series: [
                        {
                            name: 'Total',
                            color: 'rgba(115, 192, 58, 0.5)',
                            stroke: 'rgba(0,0,0,0.15)'
                        }, {
                            name: 'Unique',
                            color: 'rgba(95, 157, 47, 0.5)',
                            stroke: 'rgba(0,0,0,0.15)'
                        }
                    ]
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
                            // TODO: Remove this once moment 1.9 is released.
                            function roundToPrevious15Minutes(moment) {
                                var minutes = moment.minutes();
                                if (minutes < 15)
                                    moment.minutes(0);
                                else if (minutes < 30)
                                    moment.minutes(15);
                                else if (minutes < 45)
                                    moment.minutes(30);
                                else
                                    moment.minutes(45);

                                moment.seconds(0);

                                return moment;
                            }

                            // TODO: Remove this once moment 1.9 is released.
                            function roundToNext15Minutes(moment) {
                                var intervals = Math.floor(moment.minutes() / 15);
                                if (moment.minutes() % 15 != 0)
                                    intervals++;

                                if (intervals == 4) {
                                    moment.add('hours', 1);
                                    intervals = 0;
                                }

                                moment.minutes(intervals * 15);
                                moment.seconds(0);

                                return moment;
                            }

                            var start = roundToPrevious15Minutes(moment.unix(position.coordMinX).utc());
                            var end = roundToNext15Minutes(moment.unix(position.coordMaxX).utc());

                            // TODO: Update filter.
                            //this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

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

            getStats();
        }
    ]);
}());
