/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class FrequentViewModel extends ReportViewModelBase {
        private _pagedFrequentErrorStackStatsViewModel: stats.PagedFrequentErrorStackStatsViewModel;

        constructor(elementId: string, navigationElementId: string, chartElementId: string, projectsElementId: string, dateRangeElementId: string, frequentElementId: string, pageSize?: number, autoUpdate?: boolean) {
            super(elementId, navigationElementId, chartElementId, null, projectsElementId, dateRangeElementId, null, autoUpdate);

            this._pagedFrequentErrorStackStatsViewModel = new stats.PagedFrequentErrorStackStatsViewModel(frequentElementId, this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate);
        }

        public refreshViewModelData() { }
    }
}