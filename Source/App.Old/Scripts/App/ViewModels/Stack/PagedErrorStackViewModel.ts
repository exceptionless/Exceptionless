/// <reference path="../../exceptionless.ts" />

module exceptionless.stack {
    export class PagedErrorStackViewModel extends PagedReportViewModelBase<models.ErrorStack> {
        constructor(elementId: string, url: string, action: string, projectListViewModel: ProjectListViewModel, filterViewModel: FilterViewModel, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.ErrorStack>) {
            super(elementId, url, action, projectListViewModel, filterViewModel, pageSize, autoUpdate, data);

            this.applyBindings();
        }

        public populateResultItem(data: any): any {
            return new models.ErrorStack(data.Id, data.Type, data.Method, data.Path, data.Is404, data.Title, data.Total, DateUtil.parse(data.First), DateUtil.parse(data.Last));
        }

        public rowClick(model: models.ErrorStack, event: MouseEvent) {
            var url = '/stack/' + model.id();
            if (event.ctrlKey || event.which === 2) {
                window.open(url, '_blank');
            } else {
                window.location.href = url;
            }
        }
    }
}