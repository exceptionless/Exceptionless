/// <reference path="../../exceptionless.ts" />

module exceptionless.error {
    export class PagedErrorsByErrorStackIdViewModel extends PagedErrorsViewModel {
        constructor(elementId: string, errorStackId: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Error>) {
            super(elementId, '/error/stack/' + errorStackId, null, projectListViewModel, filterViewModel, pageSize, autoUpdate, data);
        }

        public get canRetrieve(): boolean {
            if (!this.filterViewModel)
                return false;

            return true;
        }

        public get retrieveResource(): string {
            if (!this.filterViewModel)
                return null;

            var url = this.baseUrl;
            if (!StringUtil.isNullOrEmpty(this.action))
                url += this.action;

            var page = ko.utils.unwrapObservable<number>(this.pager.currentPage);
            if (page > 1)
                url = DataUtil.updateQueryStringParameter(url, 'page', page);

            var pageSize = ko.utils.unwrapObservable<number>(this.pager.pageSize);
            if (pageSize !== 10)
                url = DataUtil.updateQueryStringParameter(url, 'pageSize', pageSize);

            var range: models.DateRange = this.filterViewModel.selectedDateRange();
            if (range.start())
                url = DataUtil.updateQueryStringParameter(url, 'start', DateUtil.formatISOString(range.start()));

            if (range.end())
                url = DataUtil.updateQueryStringParameter(url, 'end', DateUtil.formatISOString(range.end()));

            return url;
        }
    }
}