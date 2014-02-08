/// <reference path="../../exceptionless.ts" />

module exceptionless {
    export class ViewModelBase {
        private _elementId: string;
        private _baseUrl: string = '/api/v1';
        private _url: string;

        errors: any;
        loading = ko.observable<boolean>(true);
        updating = ko.observable<boolean>(false);
        spinner: Spinner;

        constructor(elementId: string, url?: string, autoUpdate?: boolean) {
            this._elementId = elementId;
            this._url = url;
            this.loading(!StringUtil.isNullOrEmpty(url));

            this.spinner = new Spinner(this.spinnerOptions);
            this.errors = ko.validation.group(this, { deep: true });

            if (!$.connection && autoUpdate) {
                window.setInterval(() => {
                    if (this.canRetrieve) {
                        this.updating(true);
                        this.refreshViewModelData();
                    }
                }, 10000);
            }

            App.onPlanChanged.subscribe(() => this.refreshViewModelData());
        }

        public refreshViewModelData() {
            this.retrieve(this.retrieveResource);
        }

        public applyBindings() {
            if (StringUtil.isNullOrEmpty(this._elementId)) {
                ko.applyBindings(this);
            } else {
                var element = document.getElementById(this._elementId);
                if (element)
                    ko.applyBindings(this, element);
                else
                    throw 'Unable to apply view model bindings. Element "' + this._elementId + '" does not exist';
            }
        }

        retrieve(resource?: string, success?: string, error?: string): void;
        retrieve(resource?: string, success?: (data: any) => void , error?: string): void;
        retrieve(resource?: string, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public retrieve(resource?: string, success?: any, error?: any) {
            if (!this.canRetrieve)
                return;

            if (resource == this._baseUrl)
                return;

            this.loading(true);

            if (!resource)
                resource = this.baseUrl;

            $.ajax(resource, {
                dataType: 'json',
                success: (data: any) => {
                    if (!success)
                        this.populateViewModel(data);
                    else if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                },
                complete: (jqXHR: JQueryXHR, textStatus: string) => {
                    this.loading(false);
                    this.updating(false);
                }
            });
        }

        insert(data: any, resource?: string, success?: string, error?: string): void;
        insert(data: any, resource?: string, success?: (data: any) => void , error?: string): void;
        insert(data: any, resource?: string, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public insert(data: any, resource?: string, success?: any, error?: any) {
            if (!resource)
                resource = this.baseUrl;

            $.ajax(resource, {
                type: 'POST',
                contentType: 'application/json;charset=utf-8',
                data: JSON.stringify(data),
                success: (data: any) => {
                    if (!success)
                        return;

                    if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                }
            });
        }

        update(resource: string, data: any, success?: string, error?: string): void;
        update(resource: string, data: any, success?: (data: any) => void , error?: string): void;
        update(resource: string, data: any, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public update(resource: string, data: any, success?: any, error?: any) {
            $.ajax(resource, {
                type: 'PUT',
                contentType: 'application/json;charset=utf-8',
                data: JSON.stringify(data),
                success: (data: any) => {
                    if (!success)
                        return;

                    if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                }
            });
        }

        patch(resource: string, data: any, success?: string, error?: string): void;
        patch(resource: string, data: any, success?: (data: any) => void , error?: string): void;
        patch(resource: string, data: any, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public patch(resource: string, data: any, success?: any, error?: any) {
            $.ajax(resource, {
                type: 'PATCH',
                contentType: 'application/json;charset=utf-8',
                data: JSON.stringify(data),
                success: (data: any) => {
                    if (!success)
                        return;

                    if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                }
            });
        }

        remove(resource: string, success?: string, error?: string): void;
        remove(resource: string, success?: (data: any) => void , error?: string): void;
        remove(resource: string, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public remove(resource: string, success?: any, error?: any) {
            $.ajax(resource, {
                type: 'DELETE',
                success: (data: any) => {
                    if (!success)
                        return;

                    if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                }
            });
        }

        public populateViewModel(data?: any) { }

        public get baseUrl(): string {
            return StringUtil.isNullOrEmpty(this._url) ? this._baseUrl : this._baseUrl + this._url;
        }

        public get canRetrieve(): boolean {
            return !StringUtil.isNullOrEmpty(this._url);
        }

        public get retrieveResource(): string {
            return null;
        }

        public get spinnerOptions(): any {
            return {
                lines: 13, // The number of lines to draw
                length: 7, // The length of each line
                width: 4, // The line thickness
                radius: 10, // The radius of the inner circle
                corners: 1, // Corner roundness (0..1)
                rotate: 0, // The rotation offset
                color: '#000', // #rgb or #rrggbb
                speed: 1, // Rounds per second
                trail: 60, // Afterglow percentage
                shadow: false, // Whether to render a shadow
                hwaccel: false, // Whether to use hardware acceleration
                className: 'spinner', // The CSS class to assign to the spinner
                zIndex: 2e9, // The z-index (defaults to 2000000000)
                top: 'auto', // Top position relative to parent in px
                left: 'auto' // Left position relative to parent in px
            };
        }
    }
}