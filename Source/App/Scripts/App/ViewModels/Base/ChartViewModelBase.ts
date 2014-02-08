/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class ChartViewModelBase extends ViewModelBase {
        private _graph: any;

        chartElementId: string;
        //chartSpinner: Spinner;
        chartTimeline: any;

        constructor (elementId: string, chartElementId: string, url: string, autoUpdate?: boolean) {
            super(elementId, url, autoUpdate);

            if (StringUtil.isNullOrEmpty(chartElementId))
                return;

            this.chartElementId = chartElementId;
            //this.chartSpinner = new Spinner(this.spinnerOptions);
            
            App.onResize.subscribe(width => {
                if (!this._graph)
                    return;

                this._graph.configure({
                    width: $(chartElementId).width(),
                    height: $(chartElementId).height()
                });

                this._graph.update();
            });

            //this.loading.subscribe((isLoading) => {
            //    if (this.updating())
            //        return;

            //    if (isLoading) {
            //        this.chartSpinner.spin($(chartElementId + '>div'));
            //    }
            //});
        }

        public get canRetrieve(): boolean {
            return !StringUtil.isNullOrEmpty(this.chartElementId);
        }

        public tryUpdateChart() {
            if (StringUtil.isNullOrEmpty(this.chartElementId))
                return;

            this.updateChart();
        }

        public updateChart() { }

        public get chart(): any {
            if (!this._graph) {
                this._graph = new Rickshaw.Graph(this.chartOptions);
                this._graph.renderer.unstack = true;
                this._graph.render();

                var hoverDetail = this.createChartHoverDetail(this._graph);
                var legend = this.createChartLegend(this._graph);
                var highlighter = this.createChartHighlight(this._graph, legend);
                this.chartTimeline = this.createChartAnnotate(this._graph);
                var rangeSelector = this.createChartRangeSelector(this._graph);

                this.renderChartAxis(this._graph);
            }

            return this._graph;
        }

        public get chartOptions(): any {
            return {
                element: document.querySelector(this.chartElementId),
                renderer: 'stack',
                stroke: true,
                padding: { top: 0.085 },
                series: []
            };
        }

        public createChartLegend(graph: any): any {
            //var legend = new Rickshaw.Graph.Legend({
            //    graph: graph,
            //    element: document.getElementById('legend'),
            //});
        }

        public createChartHighlight(graph: any, legend: any): any {
            return new Rickshaw.Graph.Behavior.Series.Highlight({
                graph: graph
                //legend: legend
            });
        }

        public createChartAnnotate(graph: any): any {
            return new Rickshaw.Graph.Annotate({
                graph: graph,
                element: document.getElementById('timeline')
            });
        }

        public createChartHoverDetail(graph: any): any {
            return new Rickshaw.Graph.HoverDetail({
                graph: graph,
                formatter: function (series, x, y, formattedX, formattedY, d) {
                    var date = moment.unix(x).utc();
                    var formattedDate = date.hours() === 0 ? DateUtil.formatWithMonthDayYear(date) : DateUtil.format(date);
                    var swatch = '<span class="detail-swatch" style="background-color: ' + series.color + '"></span>';
                    var content = swatch + series.name + ": " + numeral(y).format('0,0[.]0') + ' <br /><span class="date">' + formattedDate + '</span>';
                    return content;
                }
            });
        }

        public createChartRangeSelector(graph: any): any {
            return null;
        }

        public renderChartAxis(graph: any) {
            //var time = new Rickshaw.Fixtures.Time();
            //var xAxis = new Rickshaw.Graph.Axis.Time({
            //    graph: graph,
            //    ticksTreatment: 'glow',
            //    //timeUnit: time.unit('day')
            //});
            //xAxis.render();

            var yAxis = new Rickshaw.Graph.Axis.Y({
                graph: graph,
                ticks: 5,
                tickFormat: Rickshaw.Fixtures.Number.formatKMBT,
                ticksTreatment: 'glow'
            });

            yAxis.render();
        }
    }
}