/// <reference path="../exceptionless.ts" />

module exceptionless.error {
    export class OccurrenceNotFoundViewModel extends ViewModelBase {
        private _navigationViewModel: NavigationViewModel;

        constructor(elementId: string, navigationElementId: string, defaultProjectId?: string, autoUpdate?: boolean, data?: JSON) {
            super(elementId, null, autoUpdate);

            this._navigationViewModel = new NavigationViewModel(navigationElementId, null, defaultProjectId);
            App.onPlanChanged.subscribe(() => window.location.reload());

            App.selectedPlan.subscribe((plan: account.BillingPlan) => {
                $('#free-plan-notification').hide();
                if (plan.id === Constants.FREE_PLAN_ID)
                    $('#free-plan-notification').show();
            });

            App.selectedOrganization.subscribe(organization => {
                if (organization.isOverHourlyLimit)
                    $('#hourly-limit-notification').show();
                else
                    $('#hourly-limit-notification').hide();


                if (organization.isOverHourlyLimit)
                    $('#monthly-limit-notification').show();
                else
                    $('#monthly-limit-notification').hide();
            });

            this.applyBindings();
        }
    }
}