/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class ReportViewModelBase extends ChartViewModelBase {
        private _navigationViewModel: NavigationViewModel;
        projectListViewModel: ProjectListViewModel;
        filterViewModel: FilterViewModel;

        constructor (elementId: string, navigationElementId: string, chartElementId: string, url: string, projectsElementId: string, dateRangeElementId: string, defaultProjectId?: string, autoUpdate?: boolean) {
            super(elementId, chartElementId, url, autoUpdate);

            if (projectsElementId)
                this.projectListViewModel = new exceptionless.ProjectListViewModel(projectsElementId, defaultProjectId);
            
            if (dateRangeElementId)
                this.filterViewModel = new exceptionless.FilterViewModel(dateRangeElementId);
            
            if (navigationElementId && projectsElementId)
                this._navigationViewModel = new NavigationViewModel(navigationElementId, this.projectListViewModel);

            App.onStackUpdated.subscribe(() => {
                if (this.canRetrieve)
                    this.refreshViewModelData();
            });

            App.onErrorOccurred.subscribe(() => {
                if (this.canRetrieve)
                    this.refreshViewModelData();
            });

            App.selectedPlan.subscribe((plan: account.BillingPlan) => {
                $('#free-plan-notification').hide();
                if (plan.id === Constants.FREE_PLAN_ID)
                    $('#free-plan-notification').show();
            });
        }

        public tryUpdateChart() {
            if (!this.filterViewModel)
                return;

            super.tryUpdateChart();
        }

        public get canRetrieve(): boolean {
            if (!this.filterViewModel)
                return false;
            
            if (this.projectListViewModel) {
                if (StringUtil.isNullOrEmpty(App.selectedProject().id))
                    return false;
            }
            
            return true;
        }

        public get retrieveResource(): string {
            if (!this.filterViewModel)
                return null;

            var url = this.baseUrl;
            if (this.projectListViewModel) {
                var projectId = App.selectedProject().id;
                if (!StringUtil.isNullOrEmpty(projectId))
                    url += '/' + projectId;
                else
                    return null;
            }

            var range: models.DateRange = this.filterViewModel.selectedDateRange();
            if (range.start())
                url = DataUtil.updateQueryStringParameter(url, 'start', DateUtil.formatISOString(range.start()));

            if (range.end())
                url = DataUtil.updateQueryStringParameter(url, 'end', DateUtil.formatISOString(range.end()));

            if (this.filterViewModel.showHidden())
                url = DataUtil.updateQueryStringParameter(url, 'hidden', 'true');

            if (this.filterViewModel.showFixed())
                url = DataUtil.updateQueryStringParameter(url, 'fixed', 'true');

            if (!this.filterViewModel.showNotFound())
                url = DataUtil.updateQueryStringParameter(url, 'notfound', 'false');

            return url;
        }

        public createChartRangeSelector(graph: any): any {
            return new Rickshaw.Graph.RangeSelector({
                graph: graph,
                selectionCallback: (position: any) => {
                    var start = moment.unix(position.coordMinX).utc();
                    start.minutes(~(start.minutes() / 15) * 15);
                    start.seconds(0);

                    // TODO: If this is an end of day range, set the seconds and the minutes to 59.
                    var end = moment.unix(position.coordMaxX).utc();
                    end.minutes(~(end.minutes() / 15) / 15);
                    end.seconds(0);

                    this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

                    return false;
                }
            });
        }
    }
}