/// <reference path="../../exceptionless.ts" />

module exceptionless.user {
    export class PagedUserViewModel extends PagedViewModelBase<models.User> {
        constructor(elementId: string, url: string, action: string, pageSize?: number, autoUpdate?: boolean, data?: KnockoutObservableArray<any>) {
            super(elementId, url, action, pageSize, autoUpdate, data);
        }

        public populateResultItem(data: any): any {
            return new models.User(data.Id, data.FullName, data.EmailAddress, data.IsEmailAddressVerified, data.IsInvite, data.HasAdminRole);
        }
    }
}