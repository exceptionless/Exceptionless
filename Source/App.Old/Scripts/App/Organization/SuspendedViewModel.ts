/// <reference path="../exceptionless.ts" />

module exceptionless.organization {
    export class SuspendedViewModel extends ViewModelBase {
        private _organizationId: string;
        private _navigationViewModel: NavigationViewModel;
        
        constructor(elementId: string, navigationElementId: string, organizationId: string) {
            super(elementId);

            this._organizationId = organizationId;
            this._navigationViewModel = new NavigationViewModel(navigationElementId);
        
            ko.track(this);
            this.applyBindings();
        }

        public get isBillingRelated(): boolean {
            return App.selectedOrganization().suspensionCode === 'Billing';
        }

        public get isAbuseOrOverageOrNotActive(): boolean {
            var org: models.Organization = App.selectedOrganization();
            return org.billingStatus != enumerations.BillingStatus.Active
                || org.suspensionCode === 'Abuse'
                || org.suspensionCode === 'Overage';
        }
    }
}