/// <reference path="../../exceptionless.ts" />

module exceptionless.organization {
    export class PagedOrganizationPaymentsViewModel extends PagedViewModelBase<models.Invoice> {
        private _organizationId: string;

        constructor(elementId: string, url: string, action: string, organizationId: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);

            this._organizationId = organizationId;

            this.applyBindings();
        }

        public populateViewModel(data?: any) {
            super.populateViewModel(data);

            this.items.sort((a: models.Invoice, b: models.Invoice) => { return a.date > b.date ? 1 : -1; });
        }

        public populateResultItem(data: any): models.Invoice {
            return new models.Invoice(data.Id, data.Date, data.Paid);
        }

        public rowClick(model: models.Invoice, event: MouseEvent) {
            var url = '/organization/payment/' + model.id;
            window.open(url, '_blank');
        }

        public get selectedOrganization(): KnockoutComputed<models.Organization> {
            var organization = ko.utils.arrayFirst(App.organizations(), (o: models.Organization) => o.id === this._organizationId);
            return ko.computed(() => organization ? organization : new models.Organization('', 'Loading...', 0, 0, 0, 0), this);
        }

        public get selectedPlan(): KnockoutComputed<account.BillingPlan> {
            return ko.computed(() => this.selectedOrganization().selectedPlan, this);
        }
    }
}