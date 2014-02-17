/// <reference path="../../exceptionless.ts" />

module exceptionless.error {
    export class PagedRecentErrorsViewModel extends PagedErrorsViewModel {
        constructor(elementId: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Error>) {
            super(elementId, '/error/project/', '/recent', projectListViewModel, filterViewModel, pageSize, autoUpdate, data);
        }
    }
}