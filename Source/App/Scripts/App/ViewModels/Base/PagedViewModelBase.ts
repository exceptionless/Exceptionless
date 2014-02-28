/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class PagedViewModelBase<T> extends ViewModelBase {
        action = '';
        totalLimitedByPlan = ko.observable<boolean>(false);
        items = ko.observableArray<T>([]);
        pager: PagerViewModel;
        newItem: any;

        saveItemCommand: KoliteCommand;

        constructor(elementId: string, url: string, action?: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, autoUpdate);

            this.action = action;

            this.registerNewItemRules();

            this.pager = new PagerViewModel(elementId + '-page', 0, pageSize);
            this.pager.currentPage.subscribe(() => {
                $.scrollTo('#' + elementId, { offset: { top: -110 } });
                this.retrieve(this.retrieveResource);
            });

            if (data) {
                data.subscribe((data: any) => {
                    if (this.pager.currentPage() !== 1) // NOTE: Currently the data passed down is only ever for the first page (limitation)...
                        return;

                    this.populateViewModel(ko.mapping.toJS(data));
                    this.loading(false);
                });
            }

            if (!data || this.pager.currentPage() !== 1)
                this.retrieve(this.retrieveResource);

            this.saveItemCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    if (isExecuting || !this.newItem.isModified()) {
                        return false;
                    }

                    if (!this.newItem.isValid()) {
                        this.errors.showAllMessages();
                        return false;
                    }

                    return true;
                },
                execute: (complete) => {
                    this.insertItem(this.newItem(), complete);
                }
            });
        }

        public populateViewModel(data?: any) {
            if (!data)
                return;

            this.totalLimitedByPlan(data.TotalLimitedByPlan);

            var results = data.Results ? data.Results : data;
            this.pager.totalCount(data.TotalCount ? data.TotalCount : data.length);

            // HACK: We can remove this once ODATA supports paging.
            if (data.PageSize)
                this.pager.pageSize(data.PageSize);
            else if (results.length > this.pager.pageSize())
                this.pager.pageSize(results.length);

            var items = [];
            for (var i = 0; i < results.length && i < this.pager.pageSize(); i++) {
                if (!results[i])
                    continue;

                var item = this.populateResultItem(results[i]);
                if (item)
                    items.push(item);
            }

            this.items(items);
        }

        public populateResultItem(data: any): any { 
            return null;
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

            return url;
        }

        public addItem() {
            this.newItem('');
            this.newItem.isModified(false);
            $('#add-new-item-modal').modal({ backdrop: 'static', keyboard: true, show: true });
        }

        public removeItem(item: any) {
            return false;
        }

        public insertItem(data: any, complete: () => void) {
            this.insert({ name: data }, null, (data) => {
                    $("#add-new-item-modal").modal('hide');

                    if (data) {
                        var item = this.populateResultItem(data);
                        if (item)
                            this.items.push(item);
                    }

                    App.showSuccessNotification('Successfully saved!');
                    complete();
                },
                (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (jqXHR.status === 426) {
                        complete();
                        $("#add-new-item-modal").modal('hide');

                        var message = jqXHR.responseText;
                        try {
                            message = JSON.parse(jqXHR.responseText).Message;
                        } catch (e) { }

                        bootbox.confirm(message, 'Cancel', 'Upgrade Plan', (result: boolean) => {
                            if (result)
                                App.showChangePlanDialog();
                        });

                        return;
                    }

                    App.showErrorNotification('An error occurred while saving.');
                    complete();
                });
        }

        public applyBindings() {
            this.removeItem = <any>this.removeItem.bind(this);
            super.applyBindings();
        }

        public registerNewItemRules() {
            this.newItem = ko.observable().extend({
                required: true,
                unique: {
                    collection: this.items,
                    valueAccessor: (i: any) => i.name,
                    externalValue: ''
                }
            });
        }
    }
}