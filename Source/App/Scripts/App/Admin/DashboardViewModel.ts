/// <reference path="../exceptionless.ts" />

module exceptionless.admin {
    export class DashboardViewModel extends ViewModelBase {
        private _navigationViewModel: NavigationViewModel;
        private _adminSearchablePagedOrganizationViewModel: AdminSearchablePagedOrganizationViewModel;

        smallTotal = ko.observable<number>(0);
        smallYearlyTotal = ko.observable<number>(0);
        mediumTotal = ko.observable<number>(0);
        mediumYearlyTotal = ko.observable<number>(0);
        largeTotal = ko.observable<number>(0);
        largeYearlyTotal = ko.observable<number>(0);

        monthlyTotal = ko.observable<number>(0);
        yearlyTotal = ko.observable<number>(0);

        monthlyTotalAccounts = ko.observable<number>(0);
        yearlyTotalAccounts = ko.observable<number>(0);
        freeAccounts = ko.observable<number>(0);
        paidAccounts = ko.observable<number>(0);
        freeloaderAccounts = ko.observable<number>(0);
        suspendedAccounts = ko.observable<number>(0);

        constructor(elementId: string, navigationElementId: string, tabElementId: string, organizationsElementId: string, emailAddress: string,  string, pageSize?: number, autoUpdate?: boolean, data?: any) {
            super(elementId, '/stats/plans', autoUpdate);

            TabUtil.init(tabElementId);
            this._navigationViewModel = new NavigationViewModel(navigationElementId);
            this._adminSearchablePagedOrganizationViewModel = new AdminSearchablePagedOrganizationViewModel(organizationsElementId, '/organization', '/list', emailAddress, pageSize, autoUpdate);            

            this.applyBindings();
            this.retrieve(this.retrieveResource);
        }

        public populateViewModel(data?: any) {
            this.smallTotal(data.SmallTotal);
            this.smallYearlyTotal(data.SmallYearlyTotal);
            this.mediumTotal(data.MediumTotal);
            this.mediumYearlyTotal(data.MediumYearlyTotal);
            this.largeTotal(data.LargeTotal);
            this.largeYearlyTotal(data.LargeYearlyTotal);

            this.monthlyTotal(data.MonthlyTotal);
            this.yearlyTotal(data.YearlyTotal);

            this.monthlyTotalAccounts(data.MonthlyTotalAccounts);
            this.yearlyTotalAccounts(data.YearlyTotalAccounts);
            this.freeAccounts(data.FreeAccounts);
            this.paidAccounts(data.PaidAccounts);
            this.freeloaderAccounts(data.FreeloaderAccounts);
            this.suspendedAccounts(data.SuspendedAccounts);
        }

        public viewSuspendedAccounts(tabId: string, tabName: string) {
            $(tabId + ' a[href="' + tabName +'"]').tab('show');
            location.hash = tabName;

            if (this._adminSearchablePagedOrganizationViewModel.planSearchCriteria() != organization.PlanSearchCriteria.Suspended)
                this._adminSearchablePagedOrganizationViewModel.planSearchCriteria(organization.PlanSearchCriteria.Suspended);

            if (!StringUtil.isNullOrEmpty(this._adminSearchablePagedOrganizationViewModel.criteria()))
                this._adminSearchablePagedOrganizationViewModel.criteria('');
        }
    }
}