/// <reference path="../../exceptionless.ts" />

module exceptionless.stats {
    export class PagedFrequentErrorStackStatsViewModel extends stack.PagedErrorStackViewModel {
        constructor(elementId: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, '/stats/project/', '/frequent', projectListViewModel, filterViewModel, pageSize, autoUpdate, data);
        }
    }
}