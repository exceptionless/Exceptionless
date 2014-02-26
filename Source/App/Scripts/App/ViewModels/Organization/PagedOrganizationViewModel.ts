/// <reference path="../../exceptionless.ts" />

module exceptionless.organization {
    export class PagedOrganizationViewModel extends PagedViewModelBase<models.Organization> {
        private _emailAddress: string;

        constructor(elementId: string, url: string, action: string, emailAddress: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Organization>) {
            super(elementId, url, action, pageSize, autoUpdate, data);

            this._emailAddress = emailAddress;

            App.onOrganizationUpdated.subscribe(() => this.refreshViewModelData());
            App.onPlanChanged.subscribe(() => this.refreshViewModelData());
        }

        public populateResultItem(data: any): any {
            return new models.Organization(data.Id, data.Name, data.ProjectCount, data.StackCount, data.ErrorCount, data.TotalErrorCount, data.LastErrorDate, data.SubscribeDate, data.BillingChangeDate, data.BillingChangedByUserId, data.BillingStatus, data.BillingPrice, data.PlanId, data.CardLast4, data.StripeCustomerId, data.IsSuspended, data.SuspensionCode, data.SuspensionDate, data.SuspendedByUserId, data.SuspensionNotes);
        }

        public insertItem(data: any, completeCallback: () => void ) {
            super.insertItem(data, function () {
                completeCallback();
                App.refreshViewModelData();
            });
        }

        public removeItem(org: models.Organization): boolean {
            App.showConfirmDangerDialog('Are you sure you want to delete this organization?', 'DELETE ORGANIZATION', result => {
                if (!result)
                    return;

                var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: org.id });
                this.remove(url, () => {
                    this.items.remove(org);
                    App.showSuccessNotification('Successfully deleted the organization.');
                }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    var message = 'An error occurred while trying to delete the organization.';
                    if (jqXHR.status == 400)
                        message += '<br /> Message: ' + JSON.parse(jqXHR.responseText).Message;

                    App.showErrorNotification(message);
                });
            });

            return false;
        }

        public leaveOrganization(org: models.Organization) {
            App.showConfirmDangerDialog('Are you sure you want to leave this organization?', 'LEAVE ORGANIZATION', result => {
                if (!result)
                    return;

                var url = StringUtil.format('/api/v1/organization/{id}/removeuser', { id: org.id });
                url = DataUtil.updateQueryStringParameter(url, 'emailAddress', this._emailAddress);
                this.remove(url, () => {
                    this.items.remove(org);
                    App.showSuccessNotification('Successfully left the organization.');
                }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    var message = 'An error occurred while trying to leave the organization.';
                    if (jqXHR.status == 400)
                        message += '<br /> Message: ' + JSON.parse(jqXHR.responseText).Message;

                    App.showErrorNotification(message);
                });
            });

            return false;
        }

        public rowClick(org: models.Organization, event: MouseEvent) {
            var url = '/organization/' + org.id + '/manage#projects';
            if (event.ctrlKey || event.which === 2) {
                window.open(url, '_blank');
            } else {
                window.location.href = url;
            }
        }

        public get actionsLayoutStyle(): KnockoutComputed<string> {
            return ko.computed(() => {
                if (exceptionless.App.isPhoneLayout())
                    return 'action';

                return 'action-large';
            }, this);
        }

        public applyBindings() {
            this.removeItem = <any>this.removeItem.bind(this);
            this.leaveOrganization = <any>this.leaveOrganization.bind(this);
            super.applyBindings();
        }
    }
}