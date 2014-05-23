/// <reference path="../exceptionless.ts" />

module exceptionless.organization {
    export class EditViewModel extends ViewModelBase {
        private _organizationId: string;
        private _navigationViewModel: NavigationViewModel;
        private _pagedProjectsViewModel: project.PagedProjectInfosViewModel;
        private _pagedOrganizationUserViewModel: PagedOrganizationUserViewModel;
        private _pagedOrganizationPaymentsViewModel: PagedOrganizationPaymentsViewModel;

        name = ko.observable<string>('').extend({ required: true });
        saveCommand: KoliteCommand;

        constructor(elementId: string, navigationElementId: string, projectsElementId: string, usersElementId: string, billingElementId: string, organizationId: string, tabElementId: string, pageSize?: number, autoUpdate?: boolean, data?: any) {
            super(elementId, '/organization');

            this._organizationId = organizationId;
            this._navigationViewModel = new NavigationViewModel(navigationElementId);
            TabUtil.init(tabElementId);

            App.organizations.subscribe((organizations) => {
                $('#free-plan-notification').hide();

                var org = ko.utils.arrayFirst(organizations, (o) => (<any>o).id === organizationId);
                if (org != null && org.planId === Constants.FREE_PLAN_ID) {
                    $('#free-plan-notification').show();
                }
            });

            // TODO Optmize this into only loading the data when the tab changes and or consolidate it into one request.
            this._pagedProjectsViewModel = new project.PagedProjectInfosViewModel(projectsElementId, '/project', '/organization/' + organizationId, pageSize, autoUpdate);
            this._pagedOrganizationUserViewModel = new PagedOrganizationUserViewModel(usersElementId, '/user/organization/' + organizationId, null, organizationId, pageSize, autoUpdate);
            this._pagedOrganizationPaymentsViewModel = new PagedOrganizationPaymentsViewModel(billingElementId, '/organization/' + organizationId, '/payments', organizationId, 12, autoUpdate);

            this.saveCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    if (isExecuting || !this.name.isModified())
                        return false;

                    if (!this.name.isValid()) {
                        this.errors.showAllMessages();
                        return false;
                    }

                    return true;
                },
                execute: (complete) => {
                    var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: this._organizationId });
                    this.patch(url, { Name: this.name() },
                        (data) => {
                            this.name.isModified(false);
                            App.showSuccessNotification('Successfully saved the organization name.');
                            complete();
                        }, () => {
                            App.showErrorNotification('An error occurred while saving the organization name.');
                            complete();
                        });
                }
            });

            this.applyBindings();
            this.populateViewModel(data);

            if (data)
                this.loading(false);
        }

        public populateViewModel(data?: any) {
            if (!data)
                return;

            this.name(data.Name);
            this.name.isModified(false);
        }
    }
}