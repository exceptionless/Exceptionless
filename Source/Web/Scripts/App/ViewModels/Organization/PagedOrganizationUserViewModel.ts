/// <reference path="../../exceptionless.ts" />

module exceptionless.organization {
    export class PagedOrganizationUserViewModel extends user.PagedUserViewModel {
        private _organizationId: string;
        private _invites = [];

        constructor(elementId: string, url: string, action: string, organizationId: string, invites: any[], pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);
            this._organizationId = organizationId;
            this._invites = invites;

            this.applyBindings();
        }

        public populateViewModel(data?: any) {
            super.populateViewModel(data);

            for (var i = 0; i < this._invites.length; i++) {
                if (!ko.utils.arrayFirst(this.items(), (user: models.User) => { return user.emailAddress() === this._invites[i].EmailAddress; }))
                    this.items.push(new models.User(null, null, this._invites[i].EmailAddress, false, true));
            }

            this.items.sort((a: models.User, b: models.User) => { return a.emailAddress().toLowerCase() > b.emailAddress().toLowerCase() ? 1 : -1; });
        }

        public get name(): KnockoutComputed<string> {
            return ko.computed(() => {
                var organization = ko.utils.arrayFirst(App.organizations(), (organization: models.Organization) => organization.id === this._organizationId);
                return organization != null ? organization.name : App.selectedOrganization().name;
            }, this);
        }

        public insertItem(data: any, complete: () => void) {
            var resource = StringUtil.format('/api/v1/organization/{id}/invite', { id: this._organizationId });
            resource = DataUtil.updateQueryStringParameter(resource, 'emailAddress', this.newItem());

            this.insert(null, resource, (data) => {
                $("#add-new-item-modal").modal('hide');

                if (!data) {
                    this._invites.push(this.newItem());
                    this.items.push(new models.User(null, null, this.newItem(), false, true));
                    App.showSuccessNotification(StringUtil.format('Successfully invited {emailAddress}!', { emailAddress: this.newItem() }));
                } else {
                    var item = this.populateResultItem(data);
                    if (item)
                        this.items.push(item);

                    App.showSuccessNotification(StringUtil.format('Successfully added {emailAddress} to the organization.', { emailAddress: this.newItem() }));
                }

                complete();
            }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
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

                App.showErrorNotification(StringUtil.format('An error occurred while inviting {emailAddress} to the organization.', { emailAddress: this.newItem() }));
                complete();
            });
        }

        public resendNotification(user: models.User) {
            var resource = StringUtil.format('/api/v1/organization/{id}/invite', { id: this._organizationId });
            resource = DataUtil.updateQueryStringParameter(resource, 'emailAddress', user.emailAddress());
            this.insert(null, resource, StringUtil.format('Successfully resent the email invite to {emailAddress}!', { emailAddress: user.emailAddress() }), 'An error occurred while resending the invite email.');
        }

        public removeItem(user: models.User) {
            App.showConfirmDangerDialog('Are you sure you want to remove this user from your organization?', 'REMOVE USER', result => {
                if (!result)
                    return;

                var url = StringUtil.format('/api/v1/organization/{id}/removeuser', { id: this._organizationId });
                url = DataUtil.updateQueryStringParameter(url, 'emailAddress', user.emailAddress());
                this.remove(url, (data) => {
                    this.items.remove(user);
                    if (user.isInvite()) {
                        //this._invites.remove(user);
                        App.showSuccessNotification('Successfully revoked invite for ' + user.emailAddress());
                    } else {
                        App.showSuccessNotification('Successfully removed user ' + user.emailAddress());
                    }
                }, () => {
                    if (user.isInvite())
                        App.showErrorNotification('An error occurred while trying to revoke the invite for ' + user.emailAddress());
                    else
                        App.showErrorNotification('An error occurred while trying to remove the user ' + user.emailAddress());
                });
            });

            return false;
        }

        public applyBindings() {
            this.resendNotification = <any>this.resendNotification.bind(this);
            this.removeItem = <any>this.removeItem.bind(this);
            super.applyBindings();
        }

        public registerNewItemRules() {
            this.newItem = ko.observable().extend({
                email: true,
                required: true,
                unique: {
                    collection: this.items,
                    valueAccessor: (i: any) => i.emailAddress(),
                    externalValue: ''
                }
            });
        }
    }
}