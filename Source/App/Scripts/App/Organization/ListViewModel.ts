/// <reference path="../exceptionless.ts" />

module exceptionless.organization {
    export class ListViewModel extends PagedOrganizationViewModel {
        private _navigationViewModel: NavigationViewModel;

        constructor(elementId: string, navigationElementId: string, emailAddress: string, pageSize?: number, autoUpdate?: boolean) {
            super(elementId, '/organization', null, emailAddress, pageSize, autoUpdate);
        
            this._navigationViewModel = new NavigationViewModel(navigationElementId);

            this.applyBindings();
        }

        public populateViewModel(data?: any) {
            super.populateViewModel(data);
            this.items.sort((a: models.Organization, b: models.Organization) => {
                return a.name.toLowerCase() > b.name.toLowerCase() ? 1 : -1;
            });
        }

        public addItem() {
            var freeOrganizations = ko.utils.arrayFilter(this.items(), (o: models.Organization) => o.selectedPlan.id === Constants.FREE_PLAN_ID);
            if (freeOrganizations.length > 0) {
                bootbox.confirm('You already have one free account. You are not allowed to create more than one free account.', 'Cancel', 'Upgrade Plan', (result: boolean) => {
                    if (result) {
                        App.showChangePlanDialog(freeOrganizations[0]);
                    }
                });

                return;
            }

            super.addItem();
        }
    }
}