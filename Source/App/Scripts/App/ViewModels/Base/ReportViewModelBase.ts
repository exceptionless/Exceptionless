/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class ReportViewModelBase extends ChartViewModelBase {
        private _navigationViewModel: NavigationViewModel;
        projectListViewModel: ProjectListViewModel;
        filterViewModel: FilterViewModel;

        constructor (elementId: string, navigationElementId: string, chartElementId: string, url: string, projectsElementId: string, dateRangeElementId: string, showFilterToggleControls: boolean, defaultProjectId?: string, autoUpdate?: boolean) {
            super(elementId, chartElementId, url, autoUpdate);

            if (projectsElementId)
                this.projectListViewModel = new exceptionless.ProjectListViewModel(projectsElementId, defaultProjectId);
            
            if (dateRangeElementId)
                this.filterViewModel = new exceptionless.FilterViewModel(dateRangeElementId, showFilterToggleControls);
            
            if (navigationElementId && projectsElementId)
                this._navigationViewModel = new NavigationViewModel(navigationElementId, this.projectListViewModel);

            App.onStackUpdated.subscribe((stack) => this.onStackUpdated(stack));
            App.onNewError.subscribe((error) => this.onNewError(error));

            App.selectedPlan.subscribe((plan: account.BillingPlan) => {
                $('#free-plan-notification').hide();
                if (plan.id === Constants.FREE_PLAN_ID)
                    $('#free-plan-notification').show();
            });

            App.selectedOrganization.subscribe(organization => {
                if (organization.isOverHourlyLimit)
                    $('#hourly-limit-notification').show();
                else
                    $('#hourly-limit-notification').hide();


                if (organization.isOverHourlyLimit)
                    $('#monthly-limit-notification').show();
                else
                    $('#monthly-limit-notification').hide();
            });
        }

        public onStackUpdated(stack) {
            if (this.filterViewModel.selectedDateRange().end())
                return;

            if (stack.isHidden && !this.filterViewModel.showHidden())
                return;

            if (stack.isFixed && !this.filterViewModel.showFixed())
                return;

            if (stack.is404 && !this.filterViewModel.showNotFound())
                return;

            if (this.canRetrieve)
                this.refreshViewModelData();
        }

        public onNewError(error) {
            if (this.filterViewModel.selectedDateRange().end())
                return;

            if (error.isHidden && !this.filterViewModel.showHidden())
                return;

            if (error.isFixed && !this.filterViewModel.showFixed())
                return;

            if (error.is404 && !this.filterViewModel.showNotFound())
                return;

            if (this.canRetrieve)
                this.refreshViewModelData();
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