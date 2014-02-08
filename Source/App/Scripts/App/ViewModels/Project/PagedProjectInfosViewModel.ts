/// <reference path="../../exceptionless.ts" />

module exceptionless.project {
    export class PagedProjectInfosViewModel extends PagedProjectsViewModel {
        constructor(elementId: string, url: string, action: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);
        }

        public populateResultItem(data: any): any {
            return new models.ProjectInfo(data.Id, data.Name, data.OrganizationId, data.TimeZoneOffset, data.StackCount, data.ErrorCount, data.TotalErrorCount);
        }

        public populateViewModel(data?: any) {
            super.populateViewModel(data);
            this.items.sort((a: models.ProjectInfo, b: models.ProjectInfo) => {
                if (a.organization.name === b.organization.name) {
                    return a.name.toLowerCase() > b.name.toLowerCase() ? 1 : -1;
                } else {
                    return a.organization.name > b.organization.name ? 1 : -1;
                }
            });
        }
    }
}