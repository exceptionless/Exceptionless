/// <reference path="../../exceptionless.ts" />

module exceptionless.project {
    export class PagedProjectsViewModel extends PagedViewModelBase<models.ProjectInfo> {
        constructor(elementId: string, url: string, action: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);

            this.applyBindings();
        }

        public populateResultItem(data: any): any {
            // NOTE: Description column is never sent down with the request.
            return new models.Project(data.Id, data.OrganizationId, data.Name, data.TimeZone, data.ApiKeys, data.Configuration, data.PromotedTabs);
        }

        public populateViewModel(data?: any) {
            super.populateViewModel(data);
            this.items.sort((a: models.ProjectInfo, b: models.ProjectInfo) => { return a.name.toLowerCase() > b.name.toLowerCase() ? 1 : -1; });
        }

        public removeItem(project: models.ProjectInfo): boolean {
            App.showConfirmDangerDialog('Are you sure you want to delete this project?', 'DELETE PROJECT', result => {
                if (!result)
                    return;

                var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: project.id });
                this.remove(url, (data) => {
                    this.items.remove(project)
                    App.showSuccessNotification('Successfully deleted the project.')
                },
                'An error occurred while trying to delete the project.');
            });

            return false;
        }

        public rowClick(project: models.Project, event: MouseEvent) {
            var url = '/project/' + project.id;
            if (event.ctrlKey || event.which === 2) {
                window.open(url, '_blank');
            } else {
                window.location.href = url;
            }
        }
    }
}