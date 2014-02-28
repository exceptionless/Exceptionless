/// <reference path="../../exceptionless.ts" />

module exceptionless.error {
    export class PagedNewErrorsViewModel extends PagedErrorsViewModel {
        constructor(elementId: string, errorStackId: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Error>) {
            super(elementId, '/error/project/', '/new', projectListViewModel, filterViewModel, pageSize, autoUpdate, data);
        }
    }
}