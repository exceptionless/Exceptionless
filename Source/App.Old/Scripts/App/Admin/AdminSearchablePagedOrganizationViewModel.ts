/// <reference path="../exceptionless.ts" />

module exceptionless.admin {
    export class AdminSearchablePagedOrganizationViewModel extends organization.SearchablePagedOrganizationViewModel {
        suspendedOrganization = ko.observable<models.Organization>();
        suspensionCodes = ko.observableArray<string>(['Billing', 'Overage', 'Abuse', 'Other']);
        selectedSuspensionCode = ko.observable<string>('Other');
        suspensionNotes = ko.observable<string>();
        saveSuspendedStateCommand: KoliteCommand;

        constructor(elementId: string, url: string, action: string, emailAddress: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<models.Organization>) {
            super(elementId, url, action, emailAddress, pageSize, autoUpdate, data);

            this.saveSuspendedStateCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    if (isExecuting)
                        return false;

                    return true;
                },
                execute: (complete) => {
                    var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: this.suspendedOrganization().id });
                    var data = { IsSuspended: true, SuspensionCode: this.selectedSuspensionCode(), SuspensionNotes: this.suspensionNotes() };
                    this.patch(url, data, () => {
                        this.refreshViewModelData();
                        $('#suspend-organization-modal').modal('hide');

                        App.showSuccessNotification('Successfully removed the suspension status for this organization.');
                        complete();
                    },
                        (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                            App.showErrorNotification('An error occurred while trying to remove the suspension status for this organization.');
                            complete();
                        });
                }
            });

            this.applyBindings();
            this.refreshViewModelData();
        }

        public suspendOrganization(org: models.Organization) {
            if (!App.user().hasAdminRole)
                return;

            this.suspendedOrganization(org);
            this.suspensionNotes(org.suspensionNotes);

            if (org.isSuspended) {
                App.showConfirmDangerDialog('Are you sure you want to remove the suspension status for this organization?', 'UNSUSPEND ORGANIZATION', result=> {
                    if (!result)
                        return;

                    var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: org.id });
                    this.patch(url, { IsSuspended: false, SuspensionCode: null, SuspensionNotes: null }, () => {
                        this.refreshViewModelData();
                        App.showSuccessNotification('Successfully removed the suspension status for this organization.');
                    }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                            var message = 'An error occurred while trying to remove the suspension status for this organization.';
                            if (jqXHR.status == 400)
                                message += '<br /> Message: ' + JSON.parse(jqXHR.responseText).Message;

                            App.showErrorNotification(message);
                        });
                });
            } else {
                $('#suspend-organization-modal').modal({ backdrop: 'static', keyboard: true, show: true });
            }
        }

        public get actionsLayoutStyle(): KnockoutComputed<string> {
            return ko.computed(() => {
                if (exceptionless.App.isPhoneLayout())
                    return 'action';

                if (ko.utils.arrayFirst(this.items(), (o: models.Organization) => !StringUtil.isNullOrEmpty(o.stripeCustomerId)) != null)
                    return 'action-xlarge';

                return 'action-large';
            }, this);
        }

        public applyBindings() {
            this.suspendOrganization = <any>this.suspendOrganization.bind(this);
            super.applyBindings();
        }
    }
}