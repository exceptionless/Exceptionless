/* global Rickshaw:false */

(function () {
    'use strict';

    angular.module('angular-rickshaw', [])
        .directive('rickshaw', function($compile) {
            return {
                restrict: 'EA',
                scope: {
                    options: '=rickshawOptions',
                    series: '=rickshawSeries',
                    features: '=rickshawFeatures'
                },
                link: function(scope, element) {
                    function getSettings(el) {
                        var settings = angular.copy(scope.options);
                        settings.element = el;
                        settings.series = scope.series;
                        return settings;
                    }

                    var graph;

                    function update() {
                        var mainEl = angular.element(element);
                        mainEl.empty();
                        var graphEl = $compile('<div></div>')(scope);
                        mainEl.append(graphEl);
                        var settings = getSettings(graphEl[0]);
                        graph = new Rickshaw.Graph(settings);

                        if (scope.features && scope.features.hover) {
                            var hoverConfig = {
                                graph: graph
                            };
                            hoverConfig.xFormatter = scope.features.hover.xFormatter;
                            hoverConfig.yFormatter = scope.features.hover.yFormatter;
                            hoverConfig.formatter = scope.features.hover.formatter;
                            hoverConfig.render = scope.features.hover.render;
                            var hoverDetail = new Rickshaw.Graph.HoverDetail(hoverConfig);
                        }

                        if (scope.features && scope.features.palette) {
                            var palette = new Rickshaw.Color.Palette({scheme: scope.features.palette});
                            for (var i = 0; i < settings.series.length; i++) {
                                settings.series[i].color = palette.color();
                            }
                        }

                        if (scope.features && scope.features.range) {
                            var rangeSelector = new Rickshaw.Graph.RangeSelector({
                                graph: graph,
                                selectionCallback: scope.features.range.onSelection
                            });
                        }

                        graph.render();

                        if (scope.features && scope.features.xAxis) {
                            var xAxisConfig = {
                                graph: graph
                            };

                            if (scope.features.xAxis.timeUnit) {
                                var time = new Rickshaw.Fixtures.Time();
                                xAxisConfig.timeUnit = time.unit(scope.features.xAxis.timeUnit);
                            }

                            if (scope.features.xAxis.ticksTreatment) {
                                xAxisConfig.ticksTreatment = scope.features.xAxis.ticksTreatment;
                            }

                            var xAxis = new Rickshaw.Graph.Axis.Time(xAxisConfig);
                            xAxis.render();
                        }
                        if (scope.features && scope.features.yAxis) {
                            var yAxisConfig = {
                                graph: graph
                            };

                            if (scope.features.yAxis.ticks) {
                                yAxisConfig.ticks = scope.features.yAxis.ticks;
                            }

                            if (scope.features.yAxis.tickFormat) {
                                yAxisConfig.tickFormat = Rickshaw.Fixtures.Number[scope.features.yAxis.tickFormat];
                            }

                            if (scope.features.yAxis.ticksTreatment) {
                                yAxisConfig.ticksTreatment = scope.features.yAxis.ticksTreatment;
                            }

                            var yAxis = new Rickshaw.Graph.Axis.Y(yAxisConfig);
                            yAxis.render();
                        }

                        if (scope.features && scope.features.legend) {
                            var legendEl = $compile('<div></div>')(scope);
                            mainEl.append(legendEl);

                            var legend = new Rickshaw.Graph.Legend({
                                graph: graph,
                                element: legendEl[0]
                            });
                            if (scope.features.legend.toggle) {
                                var shelving = new Rickshaw.Graph.Behavior.Series.Toggle({
                                    graph: graph,
                                    legend: legend
                                });
                            }
                            if (scope.features.legend.highlight) {
                                var highlighter = new Rickshaw.Graph.Behavior.Series.Highlight({
                                    graph: graph,
                                    legend: legend
                                });
                            }
                        }
                    }

                    scope.$watch('options', function(newValue, oldValue) {
                        if (!angular.equals(newValue, oldValue)) {
                            update();
                        }
                    });

                    scope.$watch('series', function(newValue, oldValue) {
                        if (!angular.equals(newValue, oldValue)) {
                            update();
                        }
                    });

                    scope.$watch('features', function(newValue, oldValue) {
                        if (!angular.equals(newValue, oldValue)) {
                            update();
                        }
                    });

                    update();
                }
            };
        });
}());
