/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class PagedReportViewModelBase<T> extends PagedViewModelBase<T> {
        private _canRetrieve = true;

        filterViewModel: FilterViewModel;
        projectListViewModel: ProjectListViewModel;

        constructor(elementId: string, url: string, action: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);

            this.projectListViewModel = projectListViewModel;
            App.selectedProject.subscribe(() => {
                // If the current page is not the first page, then load the data as any passed in data will only contain the first page.
                if (App.loading() && this.pager.currentPage() > 1) {
                    this.refreshViewModelData();
                    return;
                }

                if (data)
                    this._canRetrieve = false;

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();

                if (data)
                    this._canRetrieve = true;
            });
            
            this.filterViewModel = filterViewModel;
            this.filterViewModel.selectedDateRange.subscribe(() => {
                if (data)
                    this._canRetrieve = false;

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();

                if (data)
                    this._canRetrieve = true;
            });

            this.filterViewModel.showHidden.subscribe(() => {
                if (data)
                    this._canRetrieve = false;

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();

                if (data)
                    this._canRetrieve = true;
            });

            this.filterViewModel.showFixed.subscribe(() => {
                if (data)
                    this._canRetrieve = false;

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();

                if (data)
                    this._canRetrieve = true;
            });

            this.filterViewModel.showNotFound.subscribe(() => {
                if (data)
                    this._canRetrieve = false;

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();

                if (data)
                    this._canRetrieve = true;
            });

            App.onStackUpdated.subscribe(() => {
                if (this.canRetrieve) {
                    this.updating(true);
                    this.refreshViewModelData();
                }
            });

            App.onErrorOccurred.subscribe(() => {
                if (this.canRetrieve) {
                    this.updating(true);
                    this.refreshViewModelData();
                }
            });

            App.selectedPlan.subscribe((plan: account.BillingPlan) => {
                $('#free-plan-notification').hide();
                if (plan.id === Constants.FREE_PLAN_ID)
                    $('#free-plan-notification').show();
            });
        }

        public get canRetrieve(): boolean {
            if (!this._canRetrieve || !this.filterViewModel)
                return false;
            
            if (this.projectListViewModel) {
                if (StringUtil.isNullOrEmpty(App.selectedProject().id))
                    return false;
            }
            
            return true;
        }

        public get retrieveResource(): string {
            if (!this._canRetrieve || !this.filterViewModel)
                return null;

            var url = this.baseUrl;
            if (this.projectListViewModel) {
                var projectId = App.selectedProject().id;
                if (!StringUtil.isNullOrEmpty(projectId))
                    url += projectId;
                else
                    return null;
            }
            
            if (!StringUtil.isNullOrEmpty(this.action))
                url += this.action;
            
            var page = ko.utils.unwrapObservable<number>(this.pager.currentPage);
            if (page > 1)
                url = DataUtil.updateQueryStringParameter(url, 'page', page.toString());

            var pageSize = ko.utils.unwrapObservable<number>(this.pager.pageSize);
            if (pageSize !== 10)
                url = DataUtil.updateQueryStringParameter(url, 'pageSize', pageSize.toString());

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

        public truncate(elements: HTMLElement[]) {
            if (!elements || elements.length === 0)
                return;

            $.each(elements, (index: number, element: HTMLElement) => {
                if (!(element instanceof HTMLElement))
                    return;

                if (element.querySelector('.t8-default'))
                    $(element.querySelector('.t8-default')).trunk8();
                else if (element.querySelector('.t8-lines2'))
                    $(element.querySelector('.t8-lines2')).trunk8({ lines: 2 });
                else if (element.querySelector('.t8-lines3'))
                    $(element.querySelector('.t8-lines3')).trunk8({ lines: 3 });
                else if (element.querySelector('.t8-lines4'))
                    $(element.querySelector('.t8-lines4')).trunk8({ lines: 4 });
            });
        }
    }
}