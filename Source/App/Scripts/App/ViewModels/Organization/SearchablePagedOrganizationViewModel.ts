/// <reference path="../../exceptionless.ts" />

module exceptionless.organization {
    export class SearchablePagedOrganizationViewModel extends PagedOrganizationViewModel {
        // TODO: There is a bug where the base class calls retrieve and doesn't use our retreive method. So the criteria isn't sent to the server on initial load.
        criteria = ko.observable<string>('');
        planSearchCriteria = ko.observable<PlanSearchCriteria>(PlanSearchCriteria.Any);
        sortBy = ko.observable<OrganizationSortBy>(OrganizationSortBy.Newest);
        private _canUpdatePushState: boolean = true;

        constructor(elementId: string, url: string, action: string, emailAddress: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Organization>) {
            super(elementId, url, action, emailAddress, pageSize, autoUpdate, data);
            
            var criteria: string = DataUtil.getValue(Constants.ORGANIZATION_NAME_FILTER);
            if (!StringUtil.isNullOrEmpty(criteria))
                this.criteria(criteria);

            this.criteria.subscribe(() => {
                this.updatePushState();

                if (this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();
            });

            var planSearchCriteria: string = DataUtil.getValue(Constants.PLAN_SEARCH_CRITERIA);
            if (!StringUtil.isNullOrEmpty(planSearchCriteria) && PlanSearchCriteria[planSearchCriteria])
                this.planSearchCriteria(parseInt(planSearchCriteria));
            
            this.planSearchCriteria.subscribe((value: PlanSearchCriteria) => {
                localStorage.setItem(Constants.PLAN_SEARCH_CRITERIA, value.toString());
                this.updatePushState();

                if(this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();
            });

            var sortBy: string = DataUtil.getValue(Constants.ORGANIZATION_SORT_BY);
            if (!StringUtil.isNullOrEmpty(sortBy) && OrganizationSortBy[sortBy])
                this.sortBy(parseInt(sortBy));

            this.sortBy.subscribe((value: OrganizationSortBy) => {
                localStorage.setItem(Constants.ORGANIZATION_SORT_BY, value.toString());
                this.updatePushState();

                if(this.pager.currentPage() !== 1)
                    this.pager.goToPage(1);
                else
                    this.refreshViewModelData();
            });
            
            window.addEventListener('popstate', (ev: PopStateEvent) => { if (ev.state && !ev.state.internal) this.navigate(ev); });
            
            var state = $.extend(history.state ? history.state : {}, { searchablePagedOrganizationViewModel: { criteria: this.criteria(), planSearchCriteria: this.planSearchCriteria(), sortBy: this.sortBy()} });
            history.replaceState(state, this.planSearchCriteria() + '-' + this.sortBy().toString(), this.updateNavigationUrl());

            //this.applyBindings();
            //this.refreshViewModelData();
        }

        public updatePushState() {
            if (!this._canUpdatePushState)
                return;
            
            var state = $.extend(history.state ? history.state : {}, { searchablePagedOrganizationViewModel: { criteria: this.criteria(), planSearchCriteria: this.planSearchCriteria(), sortBy: this.sortBy() } });
            history.pushState(state, this.planSearchCriteria() + '-' + this.sortBy().toString(), this.updateNavigationUrl());
        }

        public get canRetrieve(): boolean {
            if (!this._canUpdatePushState)
                return false;

            return true;
        }

        public get retrieveResource(): string {
            var url = this.baseUrl;
            if (!StringUtil.isNullOrEmpty(this.action))
                url += this.action;

            var page = ko.utils.unwrapObservable<number>(this.pager.currentPage);
            if (page > 1)
                url = DataUtil.updateQueryStringParameter(url, 'page', page);

            var pageSize = ko.utils.unwrapObservable<number>(this.pager.pageSize);
            if (pageSize !== 10)
                url = DataUtil.updateQueryStringParameter(url, 'pageSize', pageSize);

            var criteria = ko.utils.unwrapObservable<string>(this.criteria);
            if (!StringUtil.isNullOrEmpty(criteria))
                url = DataUtil.updateQueryStringParameter(url, 'criteria', criteria);

            var planSearchCriteria = ko.utils.unwrapObservable<PlanSearchCriteria>(this.planSearchCriteria);
            if (planSearchCriteria && parseInt(<any>planSearchCriteria) !== PlanSearchCriteria.Any && parseInt(<any>planSearchCriteria) !== PlanSearchCriteria.Suspended)
                url = DataUtil.updateQueryStringParameter(url, 'isPaidPlan', (parseInt(<any>planSearchCriteria) === PlanSearchCriteria.Paid).toString());

            else if (planSearchCriteria && parseInt(<any>planSearchCriteria) == PlanSearchCriteria.Suspended)
                url = DataUtil.updateQueryStringParameter(url, 'isSuspended', 'true');

            var sortBy = ko.utils.unwrapObservable<OrganizationSortBy>(this.sortBy);
            if (sortBy != null)
                url = DataUtil.updateQueryStringParameter(url, 'sortBy', sortBy);

            return url;
        }

        private navigate(ev: PopStateEvent) {
            if (!ev.state || !ev.state.searchablePagedOrganizationViewModel)
                return;

            this._canUpdatePushState = false;

            var state = ev.state.searchablePagedOrganizationViewModel;
            if (state.planSearchCriteria && this.planSearchCriteria().toString() !== state.planSearchCriteria && PlanSearchCriteria[state.planSearchCriteria])
                this.planSearchCriteria(parseInt(state.planSearchCriteria));
            
            if (this.sortBy().toString() !== state.sortBy && OrganizationSortBy[state.sortBy])
                this.sortBy(parseInt(state.sortBy));

            if (this.criteria() !== state.criteria)
                this.criteria(state.criteria);

            this._canUpdatePushState = true;
            this.refreshViewModelData();
        }

        private updateNavigationUrl(): string {
            var url = location.pathname + location.hash + location.search;
            return DataUtil.updateQueryStringParameter(url, Constants.ORGANIZATION_NAME_FILTER, this.criteria());
        }
    }

    export enum PlanSearchCriteria {
        Any = 0,
        Free = 1,
        Paid = 2,
        Suspended = 3
    }

    export enum OrganizationSortBy {
        Newest = 0,
        MostActive = 1,
        Alphabetical = 2,
    }
}