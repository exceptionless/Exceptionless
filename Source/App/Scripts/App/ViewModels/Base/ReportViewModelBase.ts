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
                    var start = DateUtil.roundToPrevious15Minutes(moment.unix(position.coordMinX).utc());
                    var end = DateUtil.roundToNext15Minutes(moment.unix(position.coordMaxX).utc());
                    this.filterViewModel.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', start, end));

                    return false;
                }
            });
        }
    }
}