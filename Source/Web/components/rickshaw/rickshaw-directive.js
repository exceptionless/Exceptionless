/* global Rickshaw:false */
// Fork of https://github.com/ngyewch/angular-rickshaw

(function () {
  'use strict';

  angular.module('angular-rickshaw', ['debounce'])
    .directive('rickshaw', ['$compile', '$window', 'debounce', function ($compile, $window, debounce) {
      return {
        restrict: 'E',
        scope: {
          options: '=options',
          features: '=features'
        },
        link: function (scope, element) {
          var graph;

          function getSettings(element) {
            var settings = angular.copy(scope.options);
            settings.element = element;
            return settings;
          }

          var create = debounce(function () {
            if (!scope.options || !scope.options.series || !scope.options.series[0].data) {
              return;
            }

            var mainElement = angular.element(element);
            mainElement.empty();
            var graphElement = $compile('<div class="chart-holder-small"></div>')(scope);
            mainElement.append(graphElement);
            var settings = getSettings(graphElement[0]);

            graph = new Rickshaw.Graph(settings);

            if (scope.features && scope.features.hover) {
              var config = {
                graph: graph,
                xFormatter: scope.features.hover.xFormatter,
                yFormatter: scope.features.hover.yFormatter,
                formatter: scope.features.hover.formatter,
                onRender: scope.features.hover.onRender
              };

              var Hover = scope.features.hover.render ? Rickshaw.Class.create(Rickshaw.Graph.HoverDetail, {render: scope.features.hover.render}) : Rickshaw.Graph.HoverDetail;
              var hoverDetail = new Hover(config);
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
              var legendElement = $compile('<div></div>')(scope);
              mainElement.append(legendElement);

              var legend = new Rickshaw.Graph.Legend({
                graph: graph,
                element: legendElement[0]
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
          }, 150);

          var seriesWatcher = scope.$watch(function () {
            return scope.options.series[0].data;
          }, function (newValue, oldValue) {
            if (!angular.equals(newValue, oldValue)) {
              create();
            }
          });

          // TODO: resize should call configure function on the graph and set the elements width and height.
          var window = angular.element($window);
          window.bind('resize', create);

          scope.$on('$destroy', function (e) {
            seriesWatcher(); // Remove watcher
            window.unbind('resize', create);
          });
        }
      };
    }]);
}());
