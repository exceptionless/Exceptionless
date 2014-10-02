/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class RecentViewModel extends ReportViewModelBase {
        private _pagedErrorsViewModel: error.PagedErrorsViewModel;

        constructor(elementId: string, navigationElementId: string, chartElementId: string, projectsElementId: string, dateRangeElementId: string, recentElementId: string, pageSize?: number, autoUpdate?: boolean) {
            super(elementId, navigationElementId, chartElementId, null, projectsElementId, dateRangeElementId, true, null, autoUpdate);
            this._pagedErrorsViewModel = new error.PagedErrorsViewModel(recentElementId, '/error/project/', '/recent', this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate);
        }

        public refreshViewModelData() { }
    }
}