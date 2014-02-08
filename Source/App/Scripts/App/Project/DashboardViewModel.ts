/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class DashboardViewModel extends ReportViewModelBase {
        private _stats: KnockoutObservableArray<any> = ko.observableArray([]);
        private _frequentErrorStacks = ko.observableArray([]);
        private _checkForErrorData = true;

        private _pagedFrequentErrorStackStatsViewModel: stats.PagedFrequentErrorStackStatsViewModel;
        private _pagedRecentErrorsViewModel: error.PagedRecentErrorsViewModel;
        private _chartOptions: any;

        total = ko.observable<number>(0);
        uniqueTotal = ko.observable<number>(0);
        newTotal = ko.observable<number>(0);
        perHourAverage = ko.observable<number>(0);

        constructor(elementId: string, navigationElementId: string, projectsElementId: string, dateRangeElementId: string, chartElementId: string, frequentErrorStackElementId: string, recentErrorStackElementId: string, pageSize?: number, autoUpdate?: boolean) {
            super(elementId, navigationElementId, chartElementId, '/stats/project', projectsElementId, dateRangeElementId, null, autoUpdate);
            this.applyBindings();
            this._stats.subscribe(() => this.tryUpdateChart());

            this.filterViewModel.selectedDateRange.subscribe(() => this.retrieve(this.retrieveResource));
            this.filterViewModel.showHidden.subscribe(() => this.retrieve(this.retrieveResource));
            this.filterViewModel.showFixed.subscribe(() => this.retrieve(this.retrieveResource));
            this.filterViewModel.showNotFound.subscribe(() => this.retrieve(this.retrieveResource));
            App.selectedProject.subscribe((project: models.ProjectInfo) => { 
                var notification = '<div class="alert in fade alert-success" style="display: block;">' +
                    '<h4>We haven\'t received any data!</h4><a href="/project/' + project.id + '/configure">Configure your client</a> and start becoming exceptionless in less than 60 seconds!</div>';

                $(Constants.NOTIFICATION_SYSTEM_ID).html(project.totalErrorCount === 0 ? notification : '');

                this.retrieve(this.retrieveResource);
            });

            this._pagedFrequentErrorStackStatsViewModel = new stats.PagedFrequentErrorStackStatsViewModel(frequentErrorStackElementId, this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate, <any>this._frequentErrorStacks);
            this._pagedRecentErrorsViewModel = new error.PagedRecentErrorsViewModel(recentErrorStackElementId, this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate);
            this.retrieve(this.retrieveResource);
        }

        public populateViewModel(data?: any) {
            this.newTotal(data.NewTotal);
            this.perHourAverage(data.PerHourAverage);
            this.total(data.Total);
            this.uniqueTotal(data.UniqueTotal);

            this._stats(data.Stats);
            this._frequentErrorStacks(ko.mapping.fromJS(data.MostFrequent));
        }

        public updateChart() {
            this.chartOptions.series[0].data = [];
            this.chartOptions.series[1].data = [];

            var stats = this._stats();
            for (var x = 0; x < stats.length; x++) {
                this.chartOptions.series[0].data.push({ x: moment.utc(stats[x].Date).unix(), y: stats[x].Total, data: stats[x] });
                this.chartOptions.series[1].data.push({ x: moment.utc(stats[x].Date).unix(), y: stats[x].UniqueTotal, data: stats[x] });
            }

            this.chart.update();
            //this.chartSpinner.stop();
        }

        public get chartOptions(): any {
            if (!this._chartOptions) {
                this._chartOptions = {
                    element: document.querySelector(this.chartElementId),
                    renderer: 'stack',
                    stroke: true,
                    padding: { top: 0.085 },
                    series: [{
                        name: 'Exceptions',
                        color: 'rgba(115, 192, 58, 0.5)',
                        stroke: 'rgba(0,0,0,0.15)',
                        data: []
                    }, {
                        name: 'Unique',
                        color: 'rgba(95, 157, 47, 0.5)',
                        stroke: 'rgba(0,0,0,0.15)',
                        data: []
                    }]
                };
            }

            return this._chartOptions;
        }

        public createChartHoverDetail(graph: any): any {
            var Hover = Rickshaw.Class.create(Rickshaw.Graph.HoverDetail, {
                render: function (args) {
                    var date = moment.unix(args.domainX).utc();
                    var formattedDate = date.hours() === 0 ? DateUtil.formatWithMonthDayYear(date) : DateUtil.format(date);
                    var content = '<div class="date">' + formattedDate + '</div>';
                    args.detail.sort(function (a, b) { return a.order - b.order }).forEach(function (d) {
                        var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                        content += swatch + numeral(d.name === 'Exceptions' ? d.value.data.Total : d.value.data.UniqueTotal).format('0,0[.]0') + ' ' + d.series.name + ' <br />';
                    }, this);

                    var xLabel = document.createElement('div');
                    xLabel.className = 'x_label';
                    xLabel.innerHTML = content;
                    this.element.appendChild(xLabel);

                    this.show();
                }
            });

            return new Hover({ graph: graph });
        }
    }
}