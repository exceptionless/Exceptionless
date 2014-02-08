/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class NewViewModel extends ReportViewModelBase {
        private _pagedErrorStackViewModel: stack.PagedErrorStackViewModel;

        constructor(elementId: string, navigationElementId: string, chartElementId: string, projectsElementId: string, dateRangeElementId: string, newestErrorStackElementId: string, pageSize?: number, autoUpdate?: boolean) {
            super(elementId, navigationElementId, chartElementId, null, projectsElementId, dateRangeElementId, null, autoUpdate);

            this._pagedErrorStackViewModel = new stack.PagedErrorStackViewModel(newestErrorStackElementId, '/stack/project/', '/new', this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate);
        }

        public refreshViewModelData() { }
    }
}