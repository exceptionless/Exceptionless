/// <reference path="../../exceptionless.ts" />

module exceptionless.error {
    export class PagedErrorsViewModel extends PagedReportViewModelBase<models.Error> {
        constructor(elementId: string, url: string, action: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, projectListViewModel, filterViewModel, pageSize, autoUpdate, data);

            this.applyBindings();
        }

        public populateResultItem(data: any): any {
            return new models.Error(data.Id, data.Type, data.Method, data.Path, data.Is404, data.Message, DateUtil.parse(data.Date))
        }

        public rowClick(model: models.Error, event: MouseEvent) {
            var url = '/error/' + model.id();
            if (event.ctrlKey || event.which === 2) {
                window.open(url, '_blank');
            } else {
                window.location.href = url;
            }
        }
    }
}