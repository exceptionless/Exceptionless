/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class PagerViewModel {
        private _id: string;
        private _items = ko.observableArray<any>();
        pageSize = ko.observable(20);
        pageSlide = ko.observable(2);

        currentPage = ko.observable<number>(1);
        totalCount = ko.observable<number>(0);

        constructor(id: string, totalCount?: number, pageSize?: number, pageSlide?: number, items?: KnockoutObservableArray<any>) {
            this._id = id;
            this._items = items;

            if (totalCount)
                this.totalCount(totalCount);
            
            if (pageSize)
                this.pageSize(pageSize);

            if (pageSlide)
                this.pageSlide(pageSlide);

            var cp = DataUtil.getQueryStringValue(id);
            if (!StringUtil.isNullOrEmpty(cp) && parseInt(cp) > 0)
                this.currentPage(parseInt(cp));

            window.addEventListener('popstate', (ev: PopStateEvent) => { if (ev.state && !ev.state.internal) this.navigate(ev); });

            var data = {};
            data[id] = { currentPage: this.currentPage(), pageSize: this.pageSize(), pageSlide: this.pageSlide() };

            var state = $.extend(history.state ? history.state : {}, data);
            history.replaceState(state, this.currentPage().toString(), this.updateNavigationUrl());
        }

        public get lastPage(): KnockoutComputed<number> {
            return ko.computed(() => {
                return this.totalCount() !== null ? Math.floor((this.totalCount() - 1) / this.pageSize()) + 1 : null;
            }, this);
        }

        public get hasNextPage(): KnockoutComputed<boolean> {
            return ko.computed(() => {
                return this.lastPage() !== null ? this.currentPage() < this.lastPage() : this.currentItemsCount() === this.pageSize();
            }, this);
        }

        public get hasPreviousPage(): KnockoutComputed<boolean> {
            return ko.computed(() => {
                return this.currentPage() > 1;
            }, this);
        }

        public get currentItemsCount(): KnockoutComputed<number> {
            return ko.computed(() => {
                return this._items() ? this._items().length : this.pageSize();
            }, this);
        }

        public get firstItemIndex(): KnockoutComputed<number> {
            return ko.computed(() => {
                return this.pageSize() * (this.currentPage() - 1) + 1;
            }, this);
        }

        public get lastItemIndex(): KnockoutComputed<number> {
            return ko.computed(() => {
                return this.firstItemIndex() + this.currentItemsCount() - 1;
            }, this);
        }
        
        public get pages(): KnockoutComputed<number[]> {
            return ko.computed(() => {
                var pageCount = this.lastPage();
                var pageFrom = Math.max(1, this.currentPage() - this.pageSlide());
                var pageTo = Math.min(pageCount, this.currentPage() + this.pageSlide());
                pageFrom = Math.max(1, Math.min(pageTo - 2 * this.pageSlide(), pageFrom));
                pageTo = Math.min(pageCount, Math.max(pageFrom + 2 * this.pageSlide(), pageTo));

                var result = [];
                for (var index = pageFrom; index <= pageTo; index++) {
                    result.push(index);
                }

                return result;
            }, this);
        }

        public goToPage(page: number) {
            if (page < 1)
                return;

            this.currentPage(page);

            var data = {};
            data[this._id] = { currentPage: this.currentPage(), pageSize: this.pageSize(), pageSlide: this.pageSlide() };

            var state = $.extend(history.state ? history.state : {}, data);
            history.pushState(state, this.currentPage().toString(), this.updateNavigationUrl());
        }

        private navigate(ev: PopStateEvent) {
            if (ev.state && ev.state[this._id]) {
                this.currentPage(ev.state[this._id].currentPage);
                this.pageSize(ev.state[this._id].pageSize);
                this.pageSlide(ev.state[this._id].pageSlide);
            }
        }

        private updateNavigationUrl(): string {
            var url = location.pathname + location.hash + location.search;
            return DataUtil.updateQueryStringParameter(url, this._id, this.currentPage() > 1 ? this.currentPage().toString() : null);
        }
    }
}