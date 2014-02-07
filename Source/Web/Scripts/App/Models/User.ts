/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class User {
        id = ko.observable<string>('').extend({ required: true });
        fullName = ko.observable<string>('').extend({ required: true });
        emailAddress = ko.observable<string>('').extend({ required: true, email: true });
        isEmailAddressVerfied = ko.observable<boolean>(false).extend({ required: true });
        isInvite = ko.observable<boolean>(false).extend({ required: true });
        hasAdminRole = ko.observable<boolean>(false).extend({ required: false });

        constructor(id: string, fullName: string, emailAddress: string, isEmailAddressVerfied: boolean, isInvite?: boolean, hasAdminRole?: boolean) {
            this.id(id);
            this.fullName(fullName);
            this.emailAddress(emailAddress);
            this.isEmailAddressVerfied(isEmailAddressVerfied);

            if (isInvite)
                this.isInvite(true);

            if (hasAdminRole)
                this.hasAdminRole(true);

            ko.validatedObservable(this);
        }
    }
}